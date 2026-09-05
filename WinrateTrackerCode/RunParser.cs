using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Reads a <c>.run</c> file into a <see cref="RunRecord" />.
///
/// This reads the raw JSON rather than going through the game's
/// <c>SaveManager.LoadRunHistory</c>. That path runs the save migration pipeline and
/// deserializes the whole run — deck, relics, and every per-floor stat block — which is
/// both far more work than a win rate needs and a source of failure on old files: the
/// archive here spans schema v8 through v10, and older files still on disk migrate on every
/// read. The fields below (result, ascension, character, room types) have been stable
/// across those versions, so reading them directly is faster *and* survives more files.
///
/// Every field is optional. A file missing something yields a record with a default
/// rather than an exception, and a file that will not parse at all yields null — one
/// unreadable run should never blank the whole screen.
/// </summary>
internal static class RunParser
{
    private const string NoEncounter = "NONE.NONE";

    /// <summary>
    /// Parse one run. Returns null for invalid data or an unidentified multiplayer player.
    /// </summary>
    /// <param name="fileName">Archive file name, used as the cache key and as the
    /// fallback source of the start time.</param>
    /// <param name="json">The file's contents.</param>
    /// <param name="localPlayerIds">Local IDs by saved platform name, captured on the main thread.</param>
    public static RunRecord? Parse(string fileName, string json,
        IReadOnlyDictionary<string, ulong>? localPlayerIds = null)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var startTime = ReadInt64(root, "start_time") ?? StartTimeFromFileName(fileName);
            if (startTime is null)
                return null;

            var playerCount = ReadArrayLength(root, "players");
            var player = ReadLocalPlayer(root, localPlayerIds);
            // An arbitrary teammate must never supply this player's character or picks.
            if (playerCount > 1 && player.ValueKind != JsonValueKind.Object)
                return null;

            CountRooms(root, out var nodes, out var actsEntered, out var counts);

            var win = ReadBoolean(root, "win");
            var killedBy = ReadString(root, "killed_by_encounter");
            var (patch, patchOrder) = PatchOf(ReadString(root, "build_id"));

            return new RunRecord
            {
                FileName = fileName,
                StartTime = startTime.Value,
                Ascension = ReadInt32(root, "ascension") ?? 0,
                Win = win,
                Abandoned = ReadBoolean(root, "was_abandoned"),
                Character = player.ValueKind == JsonValueKind.Object ? CleanId(ReadString(player, "character")) : "",
                PickedCards = ReadPicks(player, "deck"),
                PickedRelics = ReadPicks(player, "relics"),
                PlayerCount = playerCount,
                RunTimeSeconds = ReadSingle(root, "run_time") ?? 0f,
                Nodes = nodes,
                ActReached = win ? 4 : Math.Max(1, actsEntered),
                Elites = counts.Elites,
                Bosses = counts.Bosses,
                Combats = counts.Combats,
                Shops = counts.Shops,
                Rests = counts.Rests,
                Events = counts.Events,
                KilledBy = win || killedBy is null or NoEncounter ? "" : CleanId(killedBy),
                Patch = patch,
                PatchOrder = patchOrder,
            };
        }
    }

    /// <summary>
    /// Reduce a build id to the patch it belongs to: <c>v0.109.1</c> becomes
    /// <c>v0.109</c>, so a patch and its hotfixes report as one line — they are the same
    /// balance, and splitting them makes two small samples out of one useful one.
    ///
    /// The numbers come back alongside because version strings do not sort as text:
    /// <c>v0.98</c> sorts after <c>v0.100</c>. Anything that does not parse (the game's
    /// own <c>pre-v0.42</c> placeholder, or a build id from a future format) is kept
    /// verbatim and sorted to the start, since it can only be older than what we can read.
    /// </summary>
    public static (string Patch, (int Major, int Minor) Order) PatchOf(string? buildId)
    {
        if (string.IsNullOrWhiteSpace(buildId))
            return ("Unknown", (int.MinValue, int.MinValue));

        var trimmed = buildId.TrimStart('v', 'V');
        var parts = trimmed.Split('.');
        if (parts.Length >= 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var major)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor))
        {
            return ($"v{major}.{minor}", (major, minor));
        }

        return (buildId, (int.MinValue, int.MinValue));
    }

    /// <summary>
    /// Turn a model id into something readable: <c>CHARACTER.NECROBINDER</c> becomes
    /// <c>Necrobinder</c>, <c>ENCOUNTER.INFESTED_PRISMS_ELITE</c> becomes
    /// <c>Infested Prisms Elite</c>. The <c>_ELITE</c>/<c>_BOSS</c> suffix is kept: in a
    /// deaths table it is the part that says which version of the fight killed you.
    /// </summary>
    /// <summary>
    /// Floor 1 holds what the run was handed rather than what it chose: the starting deck,
    /// the starting relic, and any ascension curse. Picks begin above it.
    /// </summary>
    private const int FirstPickedFloor = 2;

    /// <summary>
    /// Ids repeat heavily — a few hundred cards across hundreds of runs — so they are
    /// pooled. Without it the archive holds one string per card per run; with it, one per
    /// distinct card. This is the same reason <see cref="RunRecord" /> keeps no deck.
    ///
    /// Not synchronised, and does not need to be: parsing only happens inside
    /// <c>RunArchive.Load</c>, which its semaphore lets one caller into at a time. The
    /// archive's own cache is a plain dictionary for the same reason.
    /// </summary>
    private static readonly Dictionary<string, string> IdPool = new(StringComparer.Ordinal);

    /// <summary>
    /// The distinct ids in one of the player's lists that were added after the run started.
    /// The player has already been selected by identity for a multiplayer run.
    /// </summary>
    private static IReadOnlyList<string> ReadPicks(JsonElement player, string property)
    {
        if (player.ValueKind != JsonValueKind.Object)
            return [];
        if (!player.TryGetProperty(property, out var entries) || entries.ValueKind != JsonValueKind.Array)
            return [];

        var picked = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;
            if ((ReadInt32(entry, "floor_added_to_deck") ?? 1) < FirstPickedFloor)
                continue;
            if (ReadString(entry, "id") is not { Length: > 0 } id)
                continue;

            var name = StripPrefix(id);
            if (seen.Add(name))
                picked.Add(Pool(name));
        }
        return picked;
    }

    /// <summary><c>CARD.SHIV</c> becomes <c>SHIV</c>, which is the key the game's own text tables use.</summary>
    private static string StripPrefix(string id)
    {
        var lastDot = id.LastIndexOf('.');
        return lastDot >= 0 ? id[(lastDot + 1)..] : id;
    }

    private static string Pool(string id)
    {
        if (IdPool.TryGetValue(id, out var pooled))
            return pooled;
        IdPool[id] = id;
        return id;
    }

    public static string CleanId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var lastDot = raw.LastIndexOf('.');
        var name = lastDot >= 0 ? raw[(lastDot + 1)..] : raw;

        var builder = new StringBuilder(name.Length);
        var startOfWord = true;
        foreach (var c in name)
        {
            if (c == '_')
            {
                builder.Append(' ');
                startOfWord = true;
                continue;
            }

            builder.Append(startOfWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            startOfWord = false;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Walk the map point history once, counting rooms by type.
    ///
    /// The history is normally an array per act, but a flat array of map points shows up
    /// in older files, so both shapes are accepted. A single map point can hold more than
    /// one room — an Unknown that resolves into an event and then a fight records both —
    /// so rooms are counted, not map points. <paramref name="nodes" /> counts map points,
    /// because that is the run's length in floors.
    /// </summary>
    private static void CountRooms(JsonElement root, out int nodes, out int actsEntered, out RoomCounts counts)
    {
        nodes = 0;
        actsEntered = 0;
        counts = default;

        if (!root.TryGetProperty("map_point_history", out var history) || history.ValueKind != JsonValueKind.Array)
            return;

        foreach (var act in history.EnumerateArray())
        {
            if (act.ValueKind != JsonValueKind.Array)
            {
                // Flat history: this element is a map point, not an act.
                nodes++;
                CountMapPoint(act, ref counts);
                actsEntered = 1;
                continue;
            }

            var actLength = act.GetArrayLength();
            if (actLength == 0)
                continue;

            actsEntered++;
            nodes += actLength;
            foreach (var mapPoint in act.EnumerateArray())
                CountMapPoint(mapPoint, ref counts);
        }
    }

    private static void CountMapPoint(JsonElement mapPoint, ref RoomCounts counts)
    {
        if (mapPoint.ValueKind != JsonValueKind.Object)
            return;
        if (!mapPoint.TryGetProperty("rooms", out var rooms) || rooms.ValueKind != JsonValueKind.Array)
            return;

        foreach (var room in rooms.EnumerateArray())
        {
            if (room.ValueKind != JsonValueKind.Object)
                continue;
            if (!room.TryGetProperty("room_type", out var type) || type.ValueKind != JsonValueKind.String)
                continue;

            switch (type.GetString())
            {
                case "monster":
                    counts.Combats++;
                    break;
                case "elite":
                    counts.Elites++;
                    counts.Combats++;
                    break;
                case "boss":
                    counts.Bosses++;
                    counts.Combats++;
                    break;
                case "shop":
                    counts.Shops++;
                    break;
                case "rest_site":
                    counts.Rests++;
                    break;
                case "event":
                case "unknown":
                    counts.Events++;
                    break;
            }
        }
    }

    private struct RoomCounts
    {
        public int Elites;
        public int Bosses;
        public int Combats;
        public int Shops;
        public int Rests;
        public int Events;
    }

    /// <summary>The game names each file after the run's start time, so the stem is a fallback.</summary>
    private static long? StartTimeFromFileName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return long.TryParse(stem, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static JsonElement ReadLocalPlayer(JsonElement root, IReadOnlyDictionary<string, ulong>? localPlayerIds)
    {
        if (!root.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array)
            return default;
        // Preserve old solo files, including runs saved without a platform or player ID.
        if (players.GetArrayLength() == 1)
            return players[0];

        var platform = ReadString(root, "platform_type") ?? "none";
        if (localPlayerIds is null || !localPlayerIds.TryGetValue(platform, out var localId))
            return default;

        foreach (var player in players.EnumerateArray())
        {
            if (player.ValueKind == JsonValueKind.Object
                && player.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.Number
                && id.TryGetUInt64(out var playerId)
                && playerId == localId)
                return player;
        }
        return default;
    }

    private static int ReadArrayLength(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;

    private static bool ReadBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt32(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static long? ReadInt64(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed)
            ? parsed
            : null;

    private static float? ReadSingle(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var parsed)
            ? parsed
            : null;
}

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// What the game knows about a card or a relic that a run file does not record: its name
/// and its rarity.
///
/// A run file stores ids — <c>SHIV</c>, <c>RING_OF_THE_SNAKE</c> — and nothing else. The
/// names live in the game's text tables and the rarities on its card and relic models, so
/// both have to be asked for.
///
/// Both are hooks rather than direct calls. Everything downstream of this file is covered
/// by tests that run without the game assembly loaded, and a static reference to
/// <c>ModelDb</c> or <c>LocString</c> would drag it in. <c>GameText</c> fills them in at
/// start-up; left unset, names fall back to the tidied-up id and rarity is simply unknown,
/// which is degraded but not broken.
/// </summary>
internal static class GameData
{
    public const string Cards = "cards";
    public const string Relics = "relics";

    /// <summary>Shown when the game cannot say what rarity something is.</summary>
    public const string UnknownRarity = "—";

    /// <summary>(table, id) to display name.</summary>
    public static Func<string, string, string?>? NameLookup;

    /// <summary>(table, id) to rarity name, e.g. <c>Uncommon</c>.</summary>
    public static Func<string, string, string?>? RarityLookup;

    public static string CardName(string id) => Name(Cards, id);

    public static string RelicName(string id) => Name(Relics, id);

    private static string Name(string table, string id)
    {
        var found = NameLookup?.Invoke(table, id);
        return string.IsNullOrWhiteSpace(found) ? RunParser.CleanId(id) : found;
    }

    public static string RarityOf(string table, string id)
    {
        var found = RarityLookup?.Invoke(table, id);
        return string.IsNullOrWhiteSpace(found) ? UnknownRarity : found;
    }

    /// <summary>
    /// Rarity order for display, weakest first, covering both cards and relics — the two
    /// vocabularies overlap and the ones that do not are simply listed in the place they
    /// belong. Sorting on this rather than alphabetically is the difference between
    /// "Common, Uncommon, Rare" and "Common, Rare, Uncommon".
    /// </summary>
    private static readonly string[] RarityOrder =
    [
        "Basic", "Starter", "Common", "Uncommon", "Rare", "Ancient",
        "Shop", "Event", "Token", "Status", "Curse", "Quest",
    ];

    /// <summary>Where a rarity sorts. Anything unrecognised goes last, in name order.</summary>
    public static int RarityRank(string rarity)
    {
        var index = Array.IndexOf(RarityOrder, rarity);
        return index < 0 ? RarityOrder.Length : index;
    }
}

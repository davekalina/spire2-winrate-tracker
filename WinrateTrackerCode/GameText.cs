using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Gives <see cref="Format" /> the game's own names for cards and relics.
///
/// A run file records ids — <c>CARD.SHIV</c>, <c>RELIC.RING_OF_THE_SNAKE</c> — and the
/// game keeps the display names in per-subject text tables under
/// <c>res://localization/&lt;language&gt;/</c>, keyed <c>&lt;ID&gt;.title</c>. Reading them
/// through <see cref="LocString" /> means the tables read in whatever language the player
/// has chosen, and that a card renamed by a patch is renamed here too.
///
/// Rarity is not in a text table — it is a property of the card and relic models, so it
/// comes from <see cref="ModelDb" />, matched on the entry part of each model's id. That is
/// the same token a run file records, so <c>CARD.SHIV</c> and the Shiv model meet at
/// <c>SHIV</c>.
///
/// Both are wired in as hooks rather than called directly because everything they feed is
/// covered by tests that run without the game assembly. Anything they cannot answer falls
/// back to the tidied-up id and an unknown rarity.
/// </summary>
internal static class GameText
{
    public static void Install()
    {
        GameData.NameLookup = NameOf;
        GameData.RarityLookup = RarityOf;
    }

    private static string? NameOf(string table, string id)
    {
        try
        {
            return LocString.GetIfExists(table, $"{id}.title")?.GetRawText();
        }
        catch (Exception exception)
        {
            // A missing table should cost the tables their nice names, nothing more.
            MainFile.Logger.Warn($"Could not read the '{table}' text table: {exception.Message}");
            GameData.NameLookup = null;
            return null;
        }
    }

    /// <summary>
    /// Rarity by id, built once on first use.
    ///
    /// Built lazily rather than at start-up: the model database fills itself from static
    /// constructors across the whole game assembly, and reading it while the mod is still
    /// initialising would see whatever happens to have registered by then.
    /// </summary>
    private static Dictionary<string, string>? _rarities;

    private static string? RarityOf(string table, string id)
    {
        try
        {
            _rarities ??= BuildRarities();
            return _rarities.GetValueOrDefault($"{table}/{id}");
        }
        catch (Exception exception)
        {
            // Losing the rarity column is a fair price for the tables still opening.
            MainFile.Logger.Warn($"Could not read card and relic rarities: {exception.Message}");
            GameData.RarityLookup = null;
            return null;
        }
    }

    private static Dictionary<string, string> BuildRarities()
    {
        var rarities = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var card in ModelDb.AllCards)
            rarities[$"{GameData.Cards}/{card.Id.Entry}"] = card.Rarity.ToString();
        foreach (var relic in ModelDb.AllRelics)
            rarities[$"{GameData.Relics}/{relic.Id.Entry}"] = relic.Rarity.ToString();
        return rarities;
    }
}

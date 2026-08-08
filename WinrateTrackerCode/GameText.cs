using MegaCrit.Sts2.Core.Localization;

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
/// This is wired in as a hook rather than called directly because everything it feeds is
/// covered by tests that run without the game assembly. Anything it cannot answer falls
/// back to the tidied-up id.
/// </summary>
internal static class GameText
{
    public static void Install() => Format.NameLookup = Lookup;

    private static string? Lookup(string table, string id)
    {
        try
        {
            return LocString.GetIfExists(table, $"{id}.title")?.GetRawText();
        }
        catch (Exception exception)
        {
            // A missing table should cost the tables their nice names, nothing more.
            MainFile.Logger.Warn($"Could not read the '{table}' text table: {exception.Message}");
            Format.NameLookup = null;
            return null;
        }
    }
}

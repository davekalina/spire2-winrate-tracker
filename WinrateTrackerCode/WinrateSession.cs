namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// What the screen remembers between visits.
///
/// The filter and the open tab are a question the player was asking, not a property of
/// the screen, so closing and reopening should not throw them away. This lasts as long as
/// the game does; it is not written to disk, so a fresh session opens on the default
/// (Ascension 10, every character, finished runs only).
/// </summary>
internal static class WinrateSession
{
    public static RunFilter Filter { get; set; } = RunFilter.Default;

    public static ReportTab Tab { get; set; } = ReportTab.Overview;
}

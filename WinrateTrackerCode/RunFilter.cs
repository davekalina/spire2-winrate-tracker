namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Which runs the tables are built from.
///
/// An abandoned run counts as a loss. Quitting a run you were losing is not a different
/// outcome from losing it, and letting abandons vanish is the easiest way to flatter a
/// win rate without noticing. The one exception is an abandon on the first floor, which
/// is a reroll rather than a run — see <see cref="IgnoreEarlyAbandons" />.
///
/// Two rules are not optional and so are not settings. <b>Co-op runs are always
/// excluded</b>: a shared win is not the same evidence about your play as a solo one, and
/// mixing them makes every rate below mean two things at once. <b>Runs are ordered by
/// start time</b>, because the block tables and the streaks are claims about sequence.
/// The screen states the co-op rule in its footer rather than dropping runs silently.
/// </summary>
internal sealed record RunFilter
{
    /// <summary>An abandon below this floor is a reroll. Floor 1 only.</summary>
    public const int EarlyAbandonFloor = 2;

    /// <summary>Ascension to report on, or null for every ascension together.</summary>
    public int? Ascension { get; init; }

    /// <summary>Character display name to report on, or null for every character.</summary>
    public string? Character { get; init; }

    /// <summary>
    /// How many days back to look, or null for the whole archive. Measured from the most
    /// recent run rather than from now, so the window does not empty itself out while the
    /// game sits on the main menu overnight.
    /// </summary>
    public int? WindowDays { get; init; }

    /// <summary>
    /// Whether abandons before <see cref="EarlyAbandonFloor" /> are dropped. Comes from
    /// the mod setting rather than a control on the screen: it is a statement about what
    /// counts as a run, which should not change between two glances at the same table.
    /// </summary>
    public bool IgnoreEarlyAbandons { get; init; } = true;

    public static RunFilter Default { get; } = new() { Ascension = 10 };

    public bool Matches(RunRecord run) =>
        run.PlayerCount <= 1
        && !(IgnoreEarlyAbandons && run.IsEarlyAbandon)
        && (Ascension is null || run.Ascension == Ascension)
        && (Character is null || run.Character == Character);

    /// <summary>The matching runs, oldest first.</summary>
    public IReadOnlyList<RunRecord> Apply(IEnumerable<RunRecord> runs)
    {
        var matching = runs.Where(Matches).OrderBy(run => run.StartTime).ToList();
        if (WindowDays is not { } days || matching.Count == 0)
            return matching;

        // Anchored to the newest matching run, not to now. Anchoring to now would mean a
        // 30-day window quietly emptying out while the game is left open, and would make
        // the same archive report differently depending on when it was opened.
        var cutoff = matching[^1].StartTime - (long)days * 24 * 60 * 60;
        return matching.Where(run => run.StartTime >= cutoff).ToList();
    }
}

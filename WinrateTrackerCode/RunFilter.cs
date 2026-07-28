namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Which runs the tables are built from.
///
/// Two rules are not optional and so are not settings. <b>Co-op runs are always
/// excluded</b>: a shared win is not the same evidence about your play as a solo one, and
/// mixing them makes every rate below mean two things at once. <b>Runs are ordered by
/// start time</b>, because the block tables and the streaks are claims about sequence.
/// The screen states the co-op rule in its footer rather than dropping runs silently.
/// </summary>
internal sealed record RunFilter
{
    /// <summary>Ascension to report on, or null for every ascension together.</summary>
    public int? Ascension { get; init; }

    /// <summary>Character display name to report on, or null for every character.</summary>
    public string? Character { get; init; }

    /// <summary>
    /// Whether abandoned runs count. Off by default: abandoning is a decision to stop, not
    /// a loss, and counting it as one understates the win rate.
    /// </summary>
    public bool IncludeAbandoned { get; init; }

    public static RunFilter Default { get; } = new() { Ascension = 10 };

    public bool Matches(RunRecord run) =>
        run.PlayerCount <= 1
        && (IncludeAbandoned || !run.Abandoned)
        && (Ascension is null || run.Ascension == Ascension)
        && (Character is null || run.Character == Character);

    /// <summary>The matching runs, oldest first.</summary>
    public IReadOnlyList<RunRecord> Apply(IEnumerable<RunRecord> runs) =>
        runs.Where(Matches).OrderBy(run => run.StartTime).ToList();
}

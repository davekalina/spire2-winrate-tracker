namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Which of the screen's four accents a value is drawn in. A tone, not a colour: the
/// exact values live in <see cref="NativeStyle" />, and this file is compiled without
/// Godot so the whole Home tab can be asserted in tests.
/// </summary>
internal enum Tone
{
    /// <summary>The header gold. A plain figure with no verdict attached.</summary>
    Neutral,

    /// <summary>Better than the thing it is measured against.</summary>
    Good,

    /// <summary>Worse.</summary>
    Bad,

    /// <summary>The chart blue. A quantity that is not a win rate and has no good side.</summary>
    Measured,

    /// <summary>Dimmed. A note beside a figure rather than a figure in its own right.</summary>
    Quiet,
}

/// <summary>
/// One of the ten most recent runs, as the hover tip reads it out. Every field is one the
/// archive already holds; nothing here is computed for the tip alone.
/// </summary>
/// <param name="When">Date and clock time, e.g. <c>2026-08-24 · 22:12</c>.</param>
/// <param name="Length">How long the run took, e.g. <c>58 min</c>.</param>
/// <param name="Outcome">What ended it — <c>Killed by Queen Boss</c>, or <c>Run won</c>.</param>
/// <param name="Detail">How far it got, e.g. <c>Act 3 · 44 floors</c>.</param>
internal sealed record HomeRun(
    bool Win,
    string Character,
    int Ascension,
    string When,
    string Length,
    string Outcome,
    string Detail);

/// <summary>One bar of the Home trend, with the tip that belongs to it.</summary>
/// <param name="Height">The bar's share of the plot, 0 to 1, against the chart's ceiling.</param>
/// <param name="Cumulative">The all-time rate at this block's end, as the same fraction of the ceiling.</param>
internal sealed record HomeTrendBar(
    string Label,
    double Height,
    double Cumulative,
    string TipHeading,
    string TipRecord,
    string TipRate,
    string TipCumulative);

/// <summary>The Home trend chart: its title, its axis, its bars, and what it means.</summary>
internal sealed record HomeTrend(
    string Title,
    IReadOnlyList<string> AxisLabels,
    IReadOnlyList<HomeTrendBar> Bars,
    IReadOnlyList<string> TipLines);

/// <summary>
/// One character's chip on the Home row. Pressing it sets the screen's character filter,
/// so <see cref="Selected" /> is not chip state — it is the filter, read back.
/// </summary>
internal sealed record HomeCharacter(
    string Character,
    string LastTenRecord,
    Tone LastTenTone,
    IReadOnlyList<bool> RecentRuns,
    string LastFifty,
    bool Selected);

/// <summary>One of the four small boxes along the bottom of Home.</summary>
/// <param name="Delta">How it compares with the period before, or empty when there is nothing to compare with.</param>
internal sealed record HomeStat(
    string Caption,
    string Value,
    Tone ValueTone,
    string Detail,
    string Delta,
    Tone DeltaTone);

/// <summary>
/// The Home tab, as text.
///
/// Home replaced the Overview because the Overview answered "what does my whole archive
/// say", which is a question that stops changing after a few hundred runs. This one
/// answers "how am I doing lately", which changes every session — the last fifty runs as
/// the headline, the trend behind it, the last ten one by one, and each character's recent
/// form beside the others.
///
/// Built here rather than in the renderer for the same reason <see cref="ReportTables" />
/// is: what a figure says is a question about the report, and keeping it out of the Godot
/// code means the whole tab can be asserted without launching the game.
/// </summary>
internal sealed record HomePanel(
    string RecentCaption,
    string RecentRecord,
    string RecentRate,
    string RecentDelta,
    Tone RecentDeltaTone,
    string RecentBaseline,
    IReadOnlyList<HomeRun> RecentRuns,
    HomeTrend? Trend,
    IReadOnlyList<HomeCharacter> Characters,
    IReadOnlyList<HomeStat> Stats)
{
    /// <summary>Nothing to draw. The screen shows its empty message instead.</summary>
    public static readonly HomePanel Empty =
        new("", "", "", "", Tone.Neutral, "", [], null, [], []);

    /// <summary>
    /// <paramref name="characterRuns" /> is the archive under every filter <em>except</em>
    /// the character one. The chips are how the character filter is set, so they have to
    /// keep showing all five while one of them is selected — read from the same runs as the
    /// report, and pressing one would leave a row of exactly one chip.
    /// </summary>
    public static HomePanel Build(
        WinrateReport report,
        IReadOnlyList<CharacterRow> characterRuns,
        string? selectedCharacter)
    {
        if (report.IsEmpty)
            return Empty;

        var recent = report.Recent;
        var delta = recent.WinRate - report.Overall.WinRate;

        return new HomePanel(
            $"Last {recent.Runs} runs",
            Format.WinLoss(recent),
            Format.Percent(recent),
            // Below the window size the "last 50" is every run there is, and a delta
            // against the all-time rate would be the figure compared with itself.
            report.HasRecentWindow ? Format.Signed(delta * 100d, 1) : "",
            ToneOf(delta),
            // Whatever the rate on the right is the rate *over*, in the same words the
            // column tips use. Under a 30-day window it is not the all-time rate.
            report.HasRecentWindow ? $"vs {Format.Percent(report.Overall)} {report.Scope}" : report.Scope,
            report.RecentRuns.Select(RunOf).ToList(),
            TrendOf(report.Trend),
            characterRuns.Select(row => CharacterOf(row, selectedCharacter)).ToList(),
            StatsOf(report));
    }

    private static Tone ToneOf(double delta) =>
        delta > 0d ? Tone.Good : delta < 0d ? Tone.Bad : Tone.Neutral;

    private static HomeRun RunOf(RunRecord run) => new(
        run.Win,
        run.Character,
        run.Ascension,
        $"{Format.Date(run.LocalStart)} · {run.LocalStart:HH:mm}",
        $"{Format.Minutes(run.RunTimeMinutes)} min",
        run.Win ? "Run won" : $"Killed by {Fallback(run.KilledBy, "something unrecorded")}",
        run.Win
            ? $"Act 3 cleared · {run.Nodes} floors"
            : $"Act {run.ActReached} · {run.Nodes} floors");

    private static string Fallback(string value, string ifEmpty) =>
        string.IsNullOrWhiteSpace(value) ? ifEmpty : value;

    /// <summary>
    /// A chip's colour follows its last ten rather than its career: five or more wins is
    /// going well whoever you are, and the point of the row is which character is hot now.
    /// </summary>
    private static HomeCharacter CharacterOf(CharacterRow row, string? selected) => new(
        row.Character,
        Format.WinLoss(row.Last10),
        row.Last10.Wins >= GoodLastTen ? Tone.Good
            : row.Last10.Wins >= FairLastTen ? Tone.Neutral
            : Tone.Bad,
        row.RecentRuns,
        $"{Format.WinLoss(row.Last50)} · {Format.WholePercent(row.Last50)}",
        string.Equals(row.Character, selected, StringComparison.Ordinal));

    private const int GoodLastTen = 5;
    private const int FairLastTen = 4;

    private static HomeTrend? TrendOf(TrendChart? chart)
    {
        if (chart is null || chart.Blocks.Count == 0)
            return null;

        var ceiling = chart.CeilingPercent / 100d;

        return new HomeTrend(
            $"Trend (last {chart.Runs} runs)",
            [
                Format.WholePercent(ceiling),
                Format.WholePercent(ceiling / 2d),
                Format.WholePercent(0d),
            ],
            chart.Blocks.Select(block => new HomeTrendBar(
                block.Label,
                Math.Clamp(block.Tally.WinRate / ceiling, 0d, 1d),
                Math.Clamp(block.CumulativeWinRate / ceiling, 0d, 1d),
                $"runs {block.Range}",
                Format.WinLoss(block.Tally),
                Format.WholePercent(block.Tally),
                $"all-time here {Format.Percent(block.CumulativeWinRate)}")).ToList(),
            [
                $"Each bar is {chart.BlockRuns} runs and shows that block's own win rate.",
                "The gold line is your all-time win rate as it stood at the end of each block.",
                "Newest on the right, at most the last 500 runs. Blocks shrink to 10 or 5 runs on a smaller archive.",
            ]);
    }

    /// <summary>
    /// The four boxes: where the month is, where the patch is, whether the last few runs
    /// went the same way, and how far runs are getting. Each is a figure and what it is
    /// better or worse than, because a rate with nothing beside it says nothing.
    /// </summary>
    private static IReadOnlyList<HomeStat> StatsOf(WinrateReport report)
    {
        var stats = new List<HomeStat>();

        if (report.Month is { } month)
            stats.Add(RateStat("This month", month));
        if (report.Patch is { } patch)
            stats.Add(RateStat("This patch", patch));
        stats.Add(StreakStat(report));
        // Floors last: it is the consolation prize for a bad month, saying whether the
        // losses are coming earlier or later than they were.
        if (report.Month is { } floors)
            stats.Add(FloorStat(floors));

        return stats;
    }

    private static HomeStat RateStat(string caption, PeriodComparison period)
    {
        var delta = period.Previous is { } previous
            ? period.Current.Tally.WinRate - previous.Tally.WinRate
            : 0d;

        // The compact label throughout: these four boxes share one row, and "Aug 2026"
        // three times over is most of the width of a box spent on the year.
        return new HomeStat(
            $"{caption} · {period.Current.Compact}",
            Format.WholePercent(period.Current.Tally),
            Tone.Neutral,
            Format.WinLoss(period.Current.Tally),
            period.Previous is { } behind
                ? $"{Format.Signed(delta * 100d, 0)} vs {behind.Compact}"
                : "",
            ToneOf(delta));
    }

    private static HomeStat FloorStat(PeriodComparison month)
    {
        var delta = month.Previous is { } previous
            ? month.Current.AverageFloors - previous.AverageFloors
            : 0d;

        return new HomeStat(
            $"Avg floors · {month.Current.Compact}",
            Format.Average(month.Current.AverageFloors),
            Tone.Measured,
            "reached",
            month.Previous is { } behind ? $"{Format.Signed(delta, 1)} vs {behind.Compact}" : "",
            ToneOf(delta));
    }

    private static HomeStat StreakStat(WinrateReport report) => new(
        "Streak",
        Format.Streak(report.CurrentStreak, report.CurrentStreakIsWin),
        report.CurrentStreak == 0 ? Tone.Neutral
            : report.CurrentStreakIsWin ? Tone.Good
            : Tone.Bad,
        "",
        report.LastRun is { } last
            ? $"best {report.LongestWinStreak} · last {Format.ShortDate(last)}"
            : $"best {report.LongestWinStreak}",
        // Not a comparison — the best streak and the last run are facts, not a verdict on
        // the current streak — so it is dimmed rather than coloured green or warm.
        Tone.Quiet);
}

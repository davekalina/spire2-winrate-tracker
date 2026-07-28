namespace WinrateTracker.WinrateTrackerCode;

/// <summary>Wins out of runs. The unit every table in the report is built from.</summary>
internal readonly record struct Tally(int Runs, int Wins)
{
    public int Losses => Runs - Wins;

    /// <summary>0 to 1. Zero runs reads as 0; callers show an em dash instead.</summary>
    public double WinRate => Runs == 0 ? 0d : (double)Wins / Runs;

    public static Tally Of(IEnumerable<RunRecord> runs)
    {
        var count = 0;
        var wins = 0;
        foreach (var run in runs)
        {
            count++;
            if (run.Win)
                wins++;
        }
        return new Tally(count, wins);
    }
}

/// <summary>One row of a block table: this block's result, and the all-time rate as of its end.</summary>
internal sealed record BlockRow(string Label, DateTime From, DateTime To, Tally Block, double CumulativeWinRate);

/// <summary>One character's record, all-time and over its own last ten runs.</summary>
internal sealed record CharacterRow(string Character, Tally All, Tally Recent, double AverageAct);

internal sealed record MonthRow(
    string Month,
    Tally Tally,
    double AverageNodes,
    double AverageAct,
    double AverageElites,
    double AverageMinutes);

internal sealed record CountRow(string Label, int Count);

/// <summary>A labelled row of pre-formatted cells, for the character-by-month grid.</summary>
internal sealed record MatrixRow(string Label, IReadOnlyList<string> Cells);

/// <summary>How many runs a trailing window covers, and how they went.</summary>
internal sealed record WindowRow(int Window, Tally Tally);

/// <summary>
/// Every table the screen shows, computed in one pass over an already-filtered,
/// oldest-first run list.
///
/// The report is rebuilt whenever the filter changes and is otherwise immutable, so the
/// four tabs cannot disagree about what the numbers are. All of it is game-independent
/// on purpose: this file is linked into the test project.
/// </summary>
internal sealed record WinrateReport
{
    /// <summary>Trailing windows shown on the Overview, largest first.</summary>
    private static readonly int[] TrailingWindowSizes = [100, 50, 25, 10];

    private const int TopDeathCount = 10;
    private const int RecentRunsPerCharacter = 10;

    public required IReadOnlyList<RunRecord> Runs { get; init; }
    public required Tally Overall { get; init; }

    /// <summary>
    /// Rolling win rate as a moving window: the last 100, 50, 25, and 10 runs. A window
    /// is omitted once it would cover the whole archive, because it would just restate
    /// <see cref="Overall" />.
    /// </summary>
    public required IReadOnlyList<WindowRow> TrailingWindows { get; init; }

    /// <summary>Consecutive runs at the end of the archive that went the same way.</summary>
    public required int CurrentStreak { get; init; }

    /// <summary>Whether <see cref="CurrentStreak" /> is a run of wins or of losses.</summary>
    public required bool CurrentStreakIsWin { get; init; }

    public required int LongestWinStreak { get; init; }

    public DateTime? FirstRun { get; init; }
    public DateTime? LastRun { get; init; }

    /// <summary>10-run blocks, newest first, each carrying the all-time rate as of its end.</summary>
    public required IReadOnlyList<BlockRow> Blocks10 { get; init; }

    /// <summary>50-run blocks, newest first.</summary>
    public required IReadOnlyList<BlockRow> Blocks50 { get; init; }

    /// <summary>Characters, best win rate first.</summary>
    public required IReadOnlyList<CharacterRow> Characters { get; init; }

    /// <summary>Months, oldest first.</summary>
    public required IReadOnlyList<MonthRow> Months { get; init; }

    /// <summary>Column headings for <see cref="CharacterByMonth" />, as <c>yyyy-MM</c>.</summary>
    public required IReadOnlyList<string> MatrixMonths { get; init; }

    /// <summary>One row per character, then a Total and a Total% row.</summary>
    public required IReadOnlyList<MatrixRow> CharacterByMonth { get; init; }

    public required IReadOnlyList<CountRow> TopDeaths { get; init; }

    /// <summary>Losses bucketed by how far the run got, Act 1 first.</summary>
    public required IReadOnlyList<CountRow> LossesByAct { get; init; }

    public bool IsEmpty => Runs.Count == 0;

    /// <param name="runs">Filtered runs, oldest first — see <see cref="RunFilter.Apply" />.</param>
    public static WinrateReport Build(IReadOnlyList<RunRecord> runs)
    {
        var losses = runs.Where(run => !run.Win).ToList();
        var (currentStreak, currentIsWin) = CurrentStreakOf(runs);
        var matrixMonths = runs.Select(run => run.Month).Distinct().OrderBy(month => month, StringComparer.Ordinal).ToList();

        return new WinrateReport
        {
            Runs = runs,
            Overall = Tally.Of(runs),
            TrailingWindows = TrailingWindowsOf(runs),
            CurrentStreak = currentStreak,
            CurrentStreakIsWin = currentIsWin,
            LongestWinStreak = LongestWinStreakOf(runs),
            FirstRun = runs.Count > 0 ? runs[0].LocalStart : null,
            LastRun = runs.Count > 0 ? runs[^1].LocalStart : null,
            Blocks10 = BlocksOf(runs, 10),
            Blocks50 = BlocksOf(runs, 50),
            Characters = CharactersOf(runs),
            Months = MonthsOf(runs),
            MatrixMonths = matrixMonths,
            CharacterByMonth = CharacterByMonthOf(runs, matrixMonths),
            TopDeaths = TopDeathsOf(losses),
            LossesByAct = LossesByActOf(losses),
        };
    }

    private static List<WindowRow> TrailingWindowsOf(IReadOnlyList<RunRecord> runs) =>
        TrailingWindowSizes
            .Where(size => runs.Count > size)
            .Select(size => new WindowRow(size, Tally.Of(Slice(runs, runs.Count - size, size))))
            .ToList();

    /// <summary>
    /// Fixed-size blocks counted from the <em>oldest</em> run forward, then reversed for
    /// display. Anchoring at the start is what lets each row carry a meaningful all-time
    /// rate: block N's cumulative column is the win rate over every run up to its end, so
    /// reading down the column shows the career average moving. Anchoring at the newest
    /// run instead would reshuffle every boundary each time a run is played.
    /// </summary>
    private static List<BlockRow> BlocksOf(IReadOnlyList<RunRecord> runs, int size)
    {
        var rows = new List<BlockRow>();
        var cumulativeWins = 0;

        for (var start = 0; start < runs.Count; start += size)
        {
            var length = Math.Min(size, runs.Count - start);
            var block = Slice(runs, start, length);
            var tally = Tally.Of(block);
            cumulativeWins += tally.Wins;

            rows.Add(new BlockRow(
                $"{start + 1}-{start + length}",
                block[0].LocalStart,
                block[^1].LocalStart,
                tally,
                (double)cumulativeWins / (start + length)));
        }

        rows.Reverse();
        return rows;
    }

    private static List<CharacterRow> CharactersOf(IReadOnlyList<RunRecord> runs) =>
        runs.GroupBy(run => run.Character)
            .Select(group =>
            {
                var all = group.ToList();
                var recent = Slice(all, Math.Max(0, all.Count - RecentRunsPerCharacter), Math.Min(RecentRunsPerCharacter, all.Count));
                return new CharacterRow(
                    group.Key,
                    Tally.Of(all),
                    Tally.Of(recent),
                    all.Average(run => run.ActReached));
            })
            .OrderByDescending(row => row.All.WinRate)
            .ThenByDescending(row => row.All.Runs)
            .ThenBy(row => row.Character, StringComparer.Ordinal)
            .ToList();

    private static List<MonthRow> MonthsOf(IReadOnlyList<RunRecord> runs) =>
        runs.GroupBy(run => run.Month)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var month = group.ToList();
                return new MonthRow(
                    group.Key,
                    Tally.Of(month),
                    month.Average(run => run.Nodes),
                    month.Average(run => run.ActReached),
                    month.Average(run => run.Elites),
                    month.Average(run => run.RunTimeMinutes));
            })
            .ToList();

    /// <summary>
    /// Characters down, months across, each cell <c>wins/runs</c>. The two trailing rows
    /// are the month totals and the month win rates, so a column can be read on its own.
    /// </summary>
    private static List<MatrixRow> CharacterByMonthOf(IReadOnlyList<RunRecord> runs, IReadOnlyList<string> months)
    {
        var rows = new List<MatrixRow>();
        if (runs.Count == 0)
            return rows;

        var byCharacterMonth = runs
            .GroupBy(run => (run.Character, run.Month))
            .ToDictionary(group => group.Key, Tally.Of);

        foreach (var character in runs.Select(run => run.Character).Distinct().OrderBy(name => name, StringComparer.Ordinal))
        {
            var cells = months
                .Select(month => byCharacterMonth.TryGetValue((character, month), out var tally)
                    ? Format.WinsOverRuns(tally)
                    : Format.Empty)
                .ToList();
            rows.Add(new MatrixRow(character, cells));
        }

        var byMonth = runs.GroupBy(run => run.Month).ToDictionary(group => group.Key, Tally.Of);
        rows.Add(new MatrixRow("Total", months.Select(month => Format.WinsOverRuns(byMonth[month])).ToList()));
        rows.Add(new MatrixRow("Total %", months.Select(month => Format.Percent(byMonth[month].WinRate)).ToList()));
        return rows;
    }

    private static List<CountRow> TopDeathsOf(IReadOnlyList<RunRecord> losses) =>
        losses.GroupBy(run => string.IsNullOrEmpty(run.KilledBy) ? "Unknown" : run.KilledBy)
            .Select(group => new CountRow(group.Key, group.Count()))
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.Label, StringComparer.Ordinal)
            .Take(TopDeathCount)
            .ToList();

    private static List<CountRow> LossesByActOf(IReadOnlyList<RunRecord> losses) =>
        losses.GroupBy(run => run.ActReached)
            .OrderBy(group => group.Key)
            .Select(group => new CountRow($"Act {group.Key}", group.Count()))
            .ToList();

    private static int LongestWinStreakOf(IReadOnlyList<RunRecord> runs)
    {
        var longest = 0;
        var current = 0;
        foreach (var run in runs)
        {
            current = run.Win ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }
        return longest;
    }

    private static (int Length, bool IsWin) CurrentStreakOf(IReadOnlyList<RunRecord> runs)
    {
        if (runs.Count == 0)
            return (0, false);

        var isWin = runs[^1].Win;
        var length = 0;
        for (var i = runs.Count - 1; i >= 0 && runs[i].Win == isWin; i--)
            length++;
        return (length, isWin);
    }

    private static List<RunRecord> Slice(IReadOnlyList<RunRecord> runs, int start, int length)
    {
        var slice = new List<RunRecord>(length);
        for (var i = start; i < start + length; i++)
            slice.Add(runs[i]);
        return slice;
    }
}

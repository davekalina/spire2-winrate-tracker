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

/// <summary>
/// A stretch of consecutive runs — a block of ten, a month, a patch — with its own
/// result and the all-time rate as of its end.
///
/// One shape for all three so the tables that show them, and the graph that plots them,
/// have one thing to understand rather than three.
/// </summary>
internal sealed record PeriodRow(
    string Label,
    DateTime From,
    DateTime To,
    Tally Tally,
    double CumulativeWinRate)
{
    /// <summary>Extra column for the months table: how far a run got, in floors.</summary>
    public double AverageFloors { get; init; }
}

/// <summary>One character's record, all-time and over its own most recent runs.</summary>
internal sealed record CharacterRow(string Character, Tally All, Tally Last10, Tally Last50);

internal sealed record CountRow(string Label, int Count);

/// <summary>
/// A labelled row of the month-by-character grid. A null cell is a month that character
/// did not play — kept as null rather than a zero tally so the table can say so.
/// </summary>
internal sealed record MatrixRow(string Label, IReadOnlyList<Tally?> Cells);

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
    public required IReadOnlyList<PeriodRow> Blocks10 { get; init; }

    /// <summary>50-run blocks, newest first.</summary>
    public required IReadOnlyList<PeriodRow> Blocks50 { get; init; }

    /// <summary>Characters, best win rate first.</summary>
    public required IReadOnlyList<CharacterRow> Characters { get; init; }

    /// <summary>Months, newest first.</summary>
    public required IReadOnlyList<PeriodRow> Months { get; init; }

    /// <summary>Game patches, newest first, hotfixes folded into their patch.</summary>
    public required IReadOnlyList<PeriodRow> Patches { get; init; }

    /// <summary>Character names, in matrix column order.</summary>
    public required IReadOnlyList<string> MatrixCharacters { get; init; }

    /// <summary>One row per month, newest first, then a Total row.</summary>
    public required IReadOnlyList<MatrixRow> MonthByCharacter { get; init; }

    public required IReadOnlyList<CountRow> TopDeaths { get; init; }

    /// <summary>Losses bucketed by how far the run got, Act 1 first.</summary>
    public required IReadOnlyList<CountRow> LossesByAct { get; init; }

    public bool IsEmpty => Runs.Count == 0;

    /// <param name="runs">Filtered runs, oldest first — see <see cref="RunFilter.Apply" />.</param>
    public static WinrateReport Build(IReadOnlyList<RunRecord> runs)
    {
        var losses = runs.Where(run => !run.Win).ToList();
        var (currentStreak, currentIsWin) = CurrentStreakOf(runs);
        var matrixCharacters = runs.Select(run => run.Character).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();

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
            Patches = PatchesOf(runs),
            MatrixCharacters = matrixCharacters,
            MonthByCharacter = MonthByCharacterOf(runs, matrixCharacters),
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
    private static List<PeriodRow> BlocksOf(IReadOnlyList<RunRecord> runs, int size)
    {
        var groups = new List<List<RunRecord>>();
        for (var start = 0; start < runs.Count; start += size)
            groups.Add(Slice(runs, start, Math.Min(size, runs.Count - start)));

        var offset = 0;
        return PeriodsOf(groups, group =>
        {
            var label = $"{offset + 1}-{offset + group.Count}";
            offset += group.Count;
            return label;
        });
    }

    /// <summary>
    /// Turn consecutive groups of runs, oldest group first, into rows carrying each
    /// group's own result and the all-time rate as of its end — then reverse them, so the
    /// table reads newest first while the cumulative column still means "as of here".
    /// </summary>
    private static List<PeriodRow> PeriodsOf(
        IReadOnlyList<List<RunRecord>> groups,
        Func<List<RunRecord>, string> label)
    {
        var rows = new List<PeriodRow>(groups.Count);
        var cumulativeWins = 0;
        var cumulativeRuns = 0;

        foreach (var group in groups)
        {
            var tally = Tally.Of(group);
            cumulativeWins += tally.Wins;
            cumulativeRuns += group.Count;

            rows.Add(new PeriodRow(
                label(group),
                group[0].LocalStart,
                group[^1].LocalStart,
                tally,
                (double)cumulativeWins / cumulativeRuns)
            {
                AverageFloors = group.Average(run => run.Nodes),
            });
        }

        rows.Reverse();
        return rows;
    }

    private static List<CharacterRow> CharactersOf(IReadOnlyList<RunRecord> runs) =>
        runs.GroupBy(run => run.Character)
            .Select(group =>
            {
                var all = group.ToList();
                return new CharacterRow(group.Key, Tally.Of(all), LastOf(all, 10), LastOf(all, 50));
            })
            .OrderByDescending(row => row.All.WinRate)
            .ThenByDescending(row => row.All.Runs)
            .ThenBy(row => row.Character, StringComparer.Ordinal)
            .ToList();

    /// <summary>The most recent <paramref name="count" /> runs, or all of them if fewer.</summary>
    private static Tally LastOf(IReadOnlyList<RunRecord> runs, int count) =>
        Tally.Of(Slice(runs, Math.Max(0, runs.Count - count), Math.Min(count, runs.Count)));

    private static List<PeriodRow> MonthsOf(IReadOnlyList<RunRecord> runs) =>
        PeriodsOf(
            runs.GroupBy(run => run.Month)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.ToList())
                .ToList(),
            group => Format.MonthName(group[0].Month));

    /// <summary>
    /// One row per patch, hotfixes folded in. Ordered by version rather than by date: a
    /// run can be played on an old build after a newer one shipped, and sorting by when
    /// it happened would interleave the patches.
    /// </summary>
    private static List<PeriodRow> PatchesOf(IReadOnlyList<RunRecord> runs) =>
        PeriodsOf(
            runs.GroupBy(run => run.Patch)
                .OrderBy(group => group.First().PatchOrder.Major)
                .ThenBy(group => group.First().PatchOrder.Minor)
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.OrderBy(run => run.StartTime).ToList())
                .ToList(),
            group => group[0].Patch);

    /// <summary>
    /// Months down, characters across, each cell a win-loss record with its rate.
    ///
    /// Months are the long axis and go down the page, which is the direction a table can
    /// grow without running out of room; characters are a fixed handful and go across.
    /// The trailing row totals each character's column so it can be read on its own.
    /// </summary>
    private static List<MatrixRow> MonthByCharacterOf(IReadOnlyList<RunRecord> runs, IReadOnlyList<string> characters)
    {
        var rows = new List<MatrixRow>();
        if (runs.Count == 0)
            return rows;

        var byMonthCharacter = runs
            .GroupBy(run => (run.Month, run.Character))
            .ToDictionary(group => group.Key, Tally.Of);

        var months = runs.Select(run => run.Month)
            .Distinct()
            .OrderByDescending(month => month, StringComparer.Ordinal);

        foreach (var month in months)
        {
            var cells = characters
                .Select(character => byMonthCharacter.TryGetValue((month, character), out var tally)
                    ? tally
                    : (Tally?)null)
                .ToList();
            rows.Add(new MatrixRow(Format.MonthName(month), cells));
        }

        var byCharacter = runs.GroupBy(run => run.Character).ToDictionary(group => group.Key, Tally.Of);
        rows.Add(new MatrixRow(
            "Total",
            characters.Select(character => (Tally?)byCharacter[character]).ToList()));
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

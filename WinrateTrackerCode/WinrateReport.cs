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

/// <summary>
/// One character's record, all-time and over its own most recent runs.
///
/// <paramref name="RecentRuns" /> is the same ten runs as <paramref name="Last10" />, kept
/// one by one and oldest first, because the pip strip shows the shape of the streak and
/// not just how it totalled.
/// </summary>
internal sealed record CharacterRow(
    string Character,
    Tally All,
    Tally Last10,
    Tally Last50,
    IReadOnlyList<bool> RecentRuns);

/// <summary>
/// One stretch of runs as the Home trend plots it: the block's own win rate as a bar, and
/// the all-time rate as of its end as a point on the line.
/// </summary>
/// <param name="Label">Short axis label — the block's first run number, with a <c>+</c> when it is not full.</param>
/// <param name="Range">The block's whole span, e.g. <c>401-442</c>, for the hover tip.</param>
internal sealed record TrendBlock(string Label, string Range, Tally Tally, double CumulativeWinRate);

/// <summary>
/// The Home trend, sized to the archive it is drawn from.
///
/// Both series are percentages, so one axis serves them: <see cref="CeilingPercent" /> is
/// the top of that axis, rounded up past the tallest bar so the best block is never drawn
/// touching the ceiling.
/// </summary>
internal sealed record TrendChart(
    int BlockRuns,
    int Runs,
    int CeilingPercent,
    IReadOnlyList<TrendBlock> Blocks);

/// <summary>
/// A period beside the one before it — this month against last month, this patch against
/// the patch before. <see cref="Previous" /> is null at the start of the archive, where
/// there is nothing to compare against and the screen says nothing rather than guessing.
/// </summary>
internal sealed record PeriodComparison(PeriodRow Current, PeriodRow? Previous);

/// <summary>
/// One named bucket of runs that is not a stretch of time — a part of the day, say. No
/// from/to and no running total, because the rows are not consecutive.
/// </summary>
internal sealed record GroupRow(string Label, Tally Tally);

/// <summary>
/// One card or relic and how the runs that picked it went. Carries the raw id as well as
/// the name, because the id is what the tables and the filters agree on; see
/// <see cref="GameData" /> for where the name and the rarity come from.
/// </summary>
internal sealed record PickRow(string Id, string Rarity, Tally Tally);

/// <summary>
/// A labelled row of the month-by-character grid. A null cell is a month that character
/// did not play — kept as null rather than a zero tally so the table can say so.
/// </summary>
internal sealed record MatrixRow(string Label, IReadOnlyList<Tally?> Cells);

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
    /// <summary>How many runs back the Home headline looks.</summary>
    public const int RecentWindow = 50;

    /// <summary>How many runs the pip strips show, on Home and in the character table.</summary>
    public const int RecentRunsPerCharacter = 10;

    public required IReadOnlyList<RunRecord> Runs { get; init; }
    public required Tally Overall { get; init; }

    /// <summary>
    /// The last <see cref="RecentWindow" /> runs, or all of them if there are fewer. The
    /// Home headline: what the archive says about how the player is going now rather than
    /// how they have gone since they started.
    /// </summary>
    public required Tally Recent { get; init; }

    /// <summary>
    /// Whether <see cref="Recent" /> is a real window or simply every run again. Below the
    /// window size the two are the same number, and a delta between them is a comparison of
    /// a figure with itself.
    /// </summary>
    public bool HasRecentWindow => Runs.Count > RecentWindow;

    /// <summary>
    /// The most recent ten runs, oldest first, kept whole. The Home pip strip needs each
    /// one on its own — its character, its ascension, and how it ended — not a tally.
    /// </summary>
    public required IReadOnlyList<RunRecord> RecentRuns { get; init; }

    /// <summary>The Home trend chart, or null when there are no runs to plot.</summary>
    public TrendChart? Trend { get; init; }

    /// <summary>The newest month against the one before it. Null when there are no runs.</summary>
    public PeriodComparison? Month { get; init; }

    /// <summary>The newest patch against the one before it. Null when there are no runs.</summary>
    public PeriodComparison? Patch { get; init; }

    /// <summary>Consecutive runs at the end of the archive that went the same way.</summary>
    public required int CurrentStreak { get; init; }

    /// <summary>Whether <see cref="CurrentStreak" /> is a run of wins or of losses.</summary>
    public required bool CurrentStreakIsWin { get; init; }

    public required int LongestWinStreak { get; init; }

    public DateTime? FirstRun { get; init; }
    public DateTime? LastRun { get; init; }

    /// <summary>50-run blocks, newest first, each carrying the all-time rate as of its end.</summary>
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

    /// <summary>Runs grouped into morning, afternoon and night. See <see cref="PartOfDay" />.</summary>
    public required IReadOnlyList<GroupRow> TimeOfDay { get; init; }

    /// <summary>The same runs cut into six four-hour blocks, for a finer look at the same question.</summary>
    public required IReadOnlyList<GroupRow> HourBlocks { get; init; }

    /// <summary>Every card picked at least once, best win rate first.</summary>
    public required IReadOnlyList<PickRow> Cards { get; init; }

    /// <summary>Every relic picked at least once, best win rate first.</summary>
    public required IReadOnlyList<PickRow> Relics { get; init; }

    public bool IsEmpty => Runs.Count == 0;

    /// <param name="runs">Filtered runs, oldest first — see <see cref="RunFilter.Apply" />.</param>
    public static WinrateReport Build(IReadOnlyList<RunRecord> runs)
    {
        var (currentStreak, currentIsWin) = CurrentStreakOf(runs);
        var matrixCharacters = runs.Select(run => run.Character).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();
        var months = MonthsOf(runs);
        var patches = PatchesOf(runs);

        return new WinrateReport
        {
            Runs = runs,
            Overall = Tally.Of(runs),
            Recent = LastOf(runs, RecentWindow),
            RecentRuns = Slice(
                runs,
                Math.Max(0, runs.Count - RecentRunsPerCharacter),
                Math.Min(RecentRunsPerCharacter, runs.Count)),
            Trend = TrendOf(runs),
            // Both lists read newest first, so the head of each is the period in progress
            // and the one behind it is what to measure against.
            Month = ComparisonOf(months),
            Patch = ComparisonOf(patches),
            CurrentStreak = currentStreak,
            CurrentStreakIsWin = currentIsWin,
            LongestWinStreak = LongestWinStreakOf(runs),
            FirstRun = runs.Count > 0 ? runs[0].LocalStart : null,
            LastRun = runs.Count > 0 ? runs[^1].LocalStart : null,
            Blocks50 = BlocksOf(runs, 50),
            Characters = CharactersOf(runs),
            Months = months,
            Patches = patches,
            MatrixCharacters = matrixCharacters,
            MonthByCharacter = MonthByCharacterOf(runs, matrixCharacters),
            TimeOfDay = TimeOfDayOf(runs),
            HourBlocks = HourBlocksOf(runs),
            Cards = PicksOf(runs, run => run.PickedCards, GameData.Cards),
            Relics = PicksOf(runs, run => run.PickedRelics, GameData.Relics),
        };
    }

    private static PeriodComparison? ComparisonOf(IReadOnlyList<PeriodRow> periods) =>
        periods.Count == 0 ? null : new PeriodComparison(periods[0], periods.Count > 1 ? periods[1] : null);

    /// <summary>How far back the trend looks, so the chart never grows past ten bars.</summary>
    private const int TrendWindow = 500;

    /// <summary>
    /// The Home trend: one bar per block of consecutive runs, with the all-time rate over
    /// the top.
    ///
    /// Three rules, each answering a way the chart could lie:
    ///
    /// <list type="bullet">
    /// <item>The block size follows the archive, so a player twenty runs in gets a trend
    /// rather than a single bar. Blocks are always anchored at the oldest run, exactly as
    /// <see cref="BlocksOf" /> anchors its tables, so a boundary never moves under a player
    /// who finished one more run.</item>
    /// <item>A trailing block holding less than half its size is dropped. Two runs at 100%
    /// would otherwise set the ceiling for every bar beside it.</item>
    /// <item>Only the newest <see cref="TrendWindow" /> runs are plotted, but the line is
    /// still the rate over <em>everything</em> up to each block — that is what makes it the
    /// all-time rate rather than the rate within the window.</item>
    /// </list>
    /// </summary>
    private static TrendChart? TrendOf(IReadOnlyList<RunRecord> runs)
    {
        if (runs.Count == 0)
            return null;

        var blockRuns = runs.Count < SmallArchive ? 5 : runs.Count < MediumArchive ? 10 : 50;

        var blocks = new List<TrendBlock>();
        var cumulativeWins = 0;
        var cumulativeRuns = 0;

        for (var start = 0; start < runs.Count; start += blockRuns)
        {
            var length = Math.Min(blockRuns, runs.Count - start);
            if (length * 2 < blockRuns)
                break;

            var tally = Tally.Of(Slice(runs, start, length));
            cumulativeWins += tally.Wins;
            cumulativeRuns += length;

            blocks.Add(new TrendBlock(
                length < blockRuns ? $"{start + 1}+" : Format.Count(start + 1),
                $"{start + 1}-{start + length}",
                tally,
                (double)cumulativeWins / cumulativeRuns));
        }

        if (blocks.Count == 0)
            return null;

        var kept = Math.Max(1, TrendWindow / blockRuns);
        if (blocks.Count > kept)
            blocks.RemoveRange(0, blocks.Count - kept);

        return new TrendChart(
            blockRuns,
            blocks.Sum(block => block.Tally.Runs),
            CeilingPercent(blocks.Max(block => block.Tally.WinRate)),
            blocks);
    }

    /// <summary>Below this many runs the trend blocks five runs at a time.</summary>
    private const int SmallArchive = 60;

    /// <summary>And below this, ten.</summary>
    private const int MediumArchive = 200;

    /// <summary>
    /// The next ten per cent strictly above the tallest bar. Strictly, so the best block is
    /// drawn short of the ceiling rather than flush against it, where it would read as
    /// having run out of chart rather than as a value.
    /// </summary>
    private static int CeilingPercent(double rate) =>
        Math.Clamp(((int)Math.Floor(rate * 100d) / 10 + 1) * 10, 10, 100);

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
                return new CharacterRow(
                    group.Key,
                    Tally.Of(all),
                    LastOf(all, RecentRunsPerCharacter),
                    LastOf(all, RecentWindow),
                    RecentOutcomesOf(all));
            })
            .OrderByDescending(row => row.All.WinRate)
            .ThenByDescending(row => row.All.Runs)
            .ThenBy(row => row.Character, StringComparer.Ordinal)
            .ToList();

    /// <summary>The most recent <paramref name="count" /> runs, or all of them if fewer.</summary>
    private static Tally LastOf(IReadOnlyList<RunRecord> runs, int count) =>
        Tally.Of(Slice(runs, Math.Max(0, runs.Count - count), Math.Min(count, runs.Count)));

    /// <summary>
    /// How the last few runs went, one by one and oldest first. A pip strip shows whether
    /// four wins out of ten were four in a row or four scattered, which the tally cannot.
    /// </summary>
    private static IReadOnlyList<bool> RecentOutcomesOf(IReadOnlyList<RunRecord> runs) =>
        Slice(
                runs,
                Math.Max(0, runs.Count - RecentRunsPerCharacter),
                Math.Min(RecentRunsPerCharacter, runs.Count))
            .Select(run => run.Win)
            .ToList();

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
            .OrderBy(month => month, StringComparer.Ordinal);

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

    /// <summary>
    /// Which part of the day an hour belongs to. The boundaries are the ordinary ones —
    /// morning runs to noon, afternoon to six — with everything from six in the evening
    /// until six in the morning counted as night, so a run started after midnight lands
    /// with the late-night runs rather than opening a new morning.
    /// </summary>
    private static string PartOfDay(int hour) => hour switch
    {
        >= 6 and < 12 => "Morning",
        >= 12 and < 18 => "Afternoon",
        _ => "Night",
    };

    private static readonly string[] PartsOfDay = ["Morning", "Afternoon", "Night"];

    /// <summary>
    /// Runs by part of the day. Every bucket is listed even when empty: a gap is itself
    /// worth seeing, and rows that come and go make the table hard to read across filters.
    /// </summary>
    private static IReadOnlyList<GroupRow> TimeOfDayOf(IReadOnlyList<RunRecord> runs)
    {
        if (runs.Count == 0)
            return [];

        var byPart = runs.ToLookup(run => PartOfDay(run.StartHour));
        return PartsOfDay.Select(part => new GroupRow(part, Tally.Of(byPart[part].ToList()))).ToList();
    }

    /// <summary>Width of each block in <see cref="HourBlocksOf" />, in hours.</summary>
    private const int HourBlockSize = 4;

    private static IReadOnlyList<GroupRow> HourBlocksOf(IReadOnlyList<RunRecord> runs)
    {
        if (runs.Count == 0)
            return [];

        var byBlock = runs.ToLookup(run => run.StartHour / HourBlockSize);
        return Enumerable
            .Range(0, 24 / HourBlockSize)
            .Select(block => new GroupRow(
                $"{block * HourBlockSize:00}:00-{block * HourBlockSize + HourBlockSize - 1:00}:59",
                Tally.Of(byBlock[block].ToList())))
            .ToList();
    }

    /// <summary>
    /// Every card or relic that was picked, with the record of the runs that picked it.
    ///
    /// Ordered by win rate, then by how many runs back it up, so of two cards at the same
    /// rate the better-evidenced one comes first. Nothing is hidden for being rare — the
    /// record is shown beside the rate, which is what says whether a rate means anything.
    /// </summary>
    private static IReadOnlyList<PickRow> PicksOf(
        IReadOnlyList<RunRecord> runs,
        Func<RunRecord, IReadOnlyList<string>> picksOf,
        string table)
    {
        var runsPerPick = new Dictionary<string, int>(StringComparer.Ordinal);
        var winsPerPick = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var run in runs)
            foreach (var id in picksOf(run))
            {
                runsPerPick[id] = runsPerPick.GetValueOrDefault(id) + 1;
                if (run.Win)
                    winsPerPick[id] = winsPerPick.GetValueOrDefault(id) + 1;
            }

        return runsPerPick
            .Select(pick => new PickRow(
                pick.Key,
                GameData.RarityOf(table, pick.Key),
                new Tally(pick.Value, winsPerPick.GetValueOrDefault(pick.Key))))
            .OrderByDescending(row => row.Tally.WinRate)
            .ThenByDescending(row => row.Tally.Runs)
            .ThenBy(row => row.Id, StringComparer.Ordinal)
            .ToList();
    }
}

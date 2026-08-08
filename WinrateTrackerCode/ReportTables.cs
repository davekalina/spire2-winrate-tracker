namespace WinrateTracker.WinrateTrackerCode;

/// <summary>A column heading, and whether its values are numbers that line up on the right.</summary>
internal sealed record TableColumn(string Header, bool RightAligned = false);

/// <summary>
/// One cell. Usually a single value, but a column can carry two related numbers — a
/// record and its rate — as separate parts.
///
/// Parts are not separate columns on purpose. A column gap is sized to separate one
/// heading's worth of data from the next, and putting a record and its own rate either
/// side of that gap spends it in the wrong place: the two look as far apart as two
/// different characters do. As parts they sit under one heading, tight together, and
/// still line up down the page because the renderer measures each part across the whole
/// column.
/// </summary>
internal sealed record TableCell(IReadOnlyList<string> Parts)
{
    public static implicit operator TableCell(string text) => new([text]);

    public static TableCell Pair(string first, string second) => new([first, second]);

    /// <summary>The whole cell as one string. For tests and diagnostics.</summary>
    public string Text => string.Join(' ', Parts);
}

/// <summary>
/// One period as the graph plots it: a bar for the wins, a point on the line for the
/// all-time rate as of that period's end. Oldest first, which is left to right.
/// </summary>
internal sealed record SeriesPoint(string Label, int Wins, int Runs, double CumulativeWinRate);

/// <summary>A titled block of rows. One or more of these makes a tab.</summary>
internal sealed record TableSection(
    string Title,
    IReadOnlyList<TableColumn> Columns,
    IReadOnlyList<IReadOnlyList<TableCell>> Rows,
    IReadOnlyList<SeriesPoint>? Series = null)
{
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>Whether this table has something worth plotting.</summary>
    public bool IsGraphable => Series is { Count: > 1 };
}

/// <summary>The tabs, in the order they appear.</summary>
internal enum ReportTab
{
    Overview,
    Splits,
    Characters,
    Cards,
    Relics,
}

/// <summary>
/// Turns a <see cref="WinrateReport" /> into the exact text of every table.
///
/// Deliberately separate from the Godot rendering: what a cell says is a question about
/// the report, and keeping it here means the whole screen's contents can be asserted in
/// tests without launching the game. The renderer's only job is where the text goes.
/// </summary>
internal static class ReportTables
{
    public static string Title(ReportTab tab) => tab switch
    {
        ReportTab.Overview => "Overview",
        ReportTab.Splits => "Splits",
        ReportTab.Characters => "Characters",
        ReportTab.Cards => "Cards",
        ReportTab.Relics => "Relics",
        _ => tab.ToString(),
    };

    /// <summary>
    /// <paramref name="picks" /> narrows the Cards and Relics tabs only. It is a separate
    /// argument rather than part of the report because it hides rows, not runs — the win
    /// rate of a card must not change according to which other cards are on screen.
    /// </summary>
    public static IReadOnlyList<TableSection> Build(
        ReportTab tab,
        WinrateReport report,
        PickFilter? picks = null) => tab switch
    {
        ReportTab.Overview => Overview(report),
        ReportTab.Splits => Splits(report),
        ReportTab.Characters => Characters(report),
        ReportTab.Cards => Picks(report, picks ?? PickFilter.Default, ReportTab.Cards),
        ReportTab.Relics => Picks(report, picks ?? PickFilter.Default, ReportTab.Relics),
        _ => [],
    };

    private static IReadOnlyList<TableSection> Overview(WinrateReport report)
    {
        if (report.IsEmpty)
            return [];

        List<IReadOnlyList<TableCell>> summary =
        [
            ["Runs", Format.Count(report.Overall.Runs)],
            ["Record", Format.WinLoss(report.Overall)],
            ["Win rate", Format.Percent(report.Overall)],
            ["Current streak", Format.Streak(report.CurrentStreak, report.CurrentStreakIsWin)],
            ["Longest win streak", Format.Count(report.LongestWinStreak)],
            ["First run", Format.Date(report.FirstRun!.Value)],
            ["Last run", Format.Date(report.LastRun!.Value)],
        ];

        List<IReadOnlyList<TableCell>> rolling =
        [
            ["All time", Format.WinLoss(report.Overall), Format.Percent(report.Overall)],
        ];
        foreach (var window in report.TrailingWindows)
            rolling.Add([$"Last {window.Window}", Format.WinLoss(window.Tally), Format.Percent(window.Tally)]);

        return
        [
            new TableSection("Overall", [new TableColumn(""), new TableColumn("", RightAligned: true)], summary),
            new TableSection(
                "Rolling win rate",
                [new TableColumn("window"), new TableColumn("W-L", RightAligned: true), new TableColumn("win%", RightAligned: true)],
                rolling),
            new TableSection(
                "Losses by act",
                [new TableColumn("act"), new TableColumn("losses", RightAligned: true)],
                report.LossesByAct.Select(row => (IReadOnlyList<TableCell>)[row.Label, Format.Count(row.Count)]).ToList()),
            new TableSection(
                "Top deaths",
                [new TableColumn("encounter"), new TableColumn("losses", RightAligned: true)],
                report.TopDeaths.Select(row => (IReadOnlyList<TableCell>)[row.Label, Format.Count(row.Count)]).ToList()),
        ];
    }

    /// <summary>
    /// Every way of cutting the archive up: first the consecutive stretches — by month, by
    /// patch, by fixed-size blocks — which are all the same shape and so all graph, then
    /// the two cuts by time of day, which are not stretches of time and do not.
    /// </summary>
    private static IReadOnlyList<TableSection> Splits(WinrateReport report)
    {
        if (report.IsEmpty)
            return [];

        return
        [
            // The month is the date, so a from and a to beside it say nothing the label
            // has not already said.
            PeriodSection("By month", report.Months, "month", withFloors: true, withDates: false),
            PeriodSection("By patch", report.Patches, "patch"),
            PeriodSection("10-run blocks", report.Blocks10, "block", withOwnRate: false),
            PeriodSection("50-run blocks", report.Blocks50, "block"),
            GroupSection("By time of day", report.TimeOfDay, "time"),
            GroupSection("Every 4 hours", report.HourBlocks, "hours"),
        ];
    }

    /// <summary>
    /// A table of named buckets. No from/to and no cumulative column: these rows are not
    /// consecutive, so a running total across them would mean nothing.
    /// </summary>
    private static TableSection GroupSection(string title, IReadOnlyList<GroupRow> groups, string labelHeader) =>
        new(
            title,
            [
                new TableColumn(labelHeader),
                new TableColumn("runs", RightAligned: true),
                new TableColumn("record", RightAligned: true),
                new TableColumn("win%", RightAligned: true),
            ],
            groups.Select(group => (IReadOnlyList<TableCell>)
            [
                group.Label,
                Format.Count(group.Tally.Runs),
                Format.WinLoss(group.Tally),
                Format.Percent(group.Tally),
            ]).ToList());

    private static TableSection PeriodSection(
        string title,
        IReadOnlyList<PeriodRow> periods,
        string labelHeader,
        bool withFloors = false,
        bool withOwnRate = true,
        bool withDates = true)
    {
        List<TableColumn> columns = [new(labelHeader)];
        if (withDates)
        {
            columns.Add(new TableColumn("from", RightAligned: true));
            columns.Add(new TableColumn("to", RightAligned: true));
        }
        columns.Add(new TableColumn("record", RightAligned: true));
        // A block of exactly ten runs needs no win% column: the record is the rate with a
        // zero after it.
        if (withOwnRate)
            columns.Add(new TableColumn("win%", RightAligned: true));
        columns.Add(new TableColumn("cumulative%", RightAligned: true));
        if (withFloors)
            columns.Add(new TableColumn("avg floors", RightAligned: true));

        var rows = periods.Select(period =>
        {
            List<TableCell> cells = [period.Label];
            if (withDates)
            {
                cells.Add(Format.ShortDate(period.From));
                cells.Add(Format.ShortDate(period.To));
            }
            cells.Add(Format.WinLoss(period.Tally));
            if (withOwnRate)
                cells.Add(Format.WholePercent(period.Tally));
            cells.Add(Format.Percent(period.CumulativeWinRate));
            if (withFloors)
                cells.Add(Format.Average(period.AverageFloors));
            return (IReadOnlyList<TableCell>)cells;
        }).ToList();

        // The table reads newest first; the graph reads left to right through time.
        var series = periods
            .Reverse()
            .Select(period => new SeriesPoint(period.Label, period.Tally.Wins, period.Tally.Runs, period.CumulativeWinRate))
            .ToList();

        return new TableSection(title, columns, rows, series);
    }

    private static IReadOnlyList<TableSection> Characters(WinrateReport report)
    {
        if (report.IsEmpty)
            return [];

        var byCharacter = new TableSection(
            "By character",
            [
                new TableColumn("character"),
                new TableColumn("runs", RightAligned: true),
                new TableColumn("all time", RightAligned: true),
                new TableColumn("last 50", RightAligned: true),
                // Ten runs needs no rate, for the same reason the 10-run blocks table
                // does without one.
                new TableColumn("last 10", RightAligned: true),
            ],
            report.Characters.Select(row => (IReadOnlyList<TableCell>)
            [
                row.Character,
                Format.Count(row.All.Runs),
                TableCell.Pair(Format.WinLoss(row.All), Format.WholePercent(row.All)),
                TableCell.Pair(Format.WinLoss(row.Last50), Format.WholePercent(row.Last50)),
                Format.WinLoss(row.Last10),
            ]).ToList());

        // One column per character, its record and rate paired inside it.
        var columns = new List<TableColumn> { new("month") };
        columns.AddRange(report.MatrixCharacters.Select(character => new TableColumn(character, RightAligned: true)));

        var matrix = new TableSection(
            "Character by Month",
            columns,
            report.MonthByCharacter.Select(row =>
            {
                var cells = new List<TableCell> { row.Label };
                foreach (var tally in row.Cells)
                    cells.Add(tally is { } played
                        ? TableCell.Pair(Format.WinLoss(played), Format.WholePercent(played))
                        : TableCell.Pair(Format.Empty, ""));
                return (IReadOnlyList<TableCell>)cells;
            }).ToList());

        return [byCharacter, matrix];
    }

    /// <summary>
    /// What picking each card, or each relic, was worth.
    ///
    /// Every pick the filtered runs made, best win rate first. The starting deck and
    /// starting relic are not picks and are left out upstream, in
    /// <see cref="RunRecord.PickedCards" />.
    ///
    /// The pick count sits beside the rate on purpose. Sorted by rate alone the head of the
    /// list is whatever was picked once and won, so the column that says how many runs are
    /// behind a number has to be right there next to it.
    ///
    /// The table carries no heading of its own: the tab is already named for it, and a
    /// second "Cards" in gold under the Cards tab says nothing.
    /// </summary>
    private static IReadOnlyList<TableSection> Picks(WinrateReport report, PickFilter filter, ReportTab tab)
    {
        if (report.IsEmpty)
            return [];

        var section = tab == ReportTab.Cards
            ? PickSection("card", filter.ApplyToCards(report.Cards), GameData.CardName)
            : PickSection("relic", filter.ApplyToRelics(report.Relics), GameData.RelicName);

        // Runs from before the mod could read decks, a run filter that leaves only such
        // runs, or a minimum that nothing clears: all of them can empty the list, and the
        // tab should then say so rather than draw an empty frame.
        return section.IsEmpty ? [] : [section];
    }

    private static TableSection PickSection(
        string labelHeader,
        IReadOnlyList<PickRow> picks,
        Func<string, string> nameOf) =>
        new(
            "",
            [
                new TableColumn(labelHeader),
                new TableColumn("rarity"),
                new TableColumn("picked", RightAligned: true),
                new TableColumn("record", RightAligned: true),
                new TableColumn("win%", RightAligned: true),
            ],
            picks.Select(pick => (IReadOnlyList<TableCell>)
            [
                nameOf(pick.Id),
                pick.Rarity,
                Format.Count(pick.Tally.Runs),
                Format.WinLoss(pick.Tally),
                Format.Percent(pick.Tally),
            ]).ToList());
}

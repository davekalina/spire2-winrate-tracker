namespace WinrateTracker.WinrateTrackerCode;

/// <summary>A column heading, and whether its values are numbers that line up on the right.</summary>
internal sealed record TableColumn(string Header, bool RightAligned = false);

/// <summary>
/// One period as the graph plots it: a bar for the wins, a point on the line for the
/// all-time rate as of that period's end. Oldest first, which is left to right.
/// </summary>
internal sealed record SeriesPoint(string Label, int Wins, int Runs, double CumulativeWinRate);

/// <summary>A titled block of rows. One or more of these makes a tab.</summary>
internal sealed record TableSection(
    string Title,
    IReadOnlyList<TableColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyList<SeriesPoint>? Series = null)
{
    /// <summary>
    /// An optional row above the column headers, one entry per column, blank where a
    /// column has no group. It lets a pair of columns sit under one heading — the
    /// month-by-character grid gives each character one name over its record and rate.
    /// </summary>
    public IReadOnlyList<string>? GroupHeaders { get; init; }

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
        _ => tab.ToString(),
    };

    public static IReadOnlyList<TableSection> Build(ReportTab tab, WinrateReport report) => tab switch
    {
        ReportTab.Overview => Overview(report),
        ReportTab.Splits => Splits(report),
        ReportTab.Characters => Characters(report),
        _ => [],
    };

    private static IReadOnlyList<TableSection> Overview(WinrateReport report)
    {
        if (report.IsEmpty)
            return [];

        List<IReadOnlyList<string>> summary =
        [
            ["Runs", Format.Count(report.Overall.Runs)],
            ["Record", Format.WinLoss(report.Overall)],
            ["Win rate", Format.Percent(report.Overall)],
            ["Current streak", Format.Streak(report.CurrentStreak, report.CurrentStreakIsWin)],
            ["Longest win streak", Format.Count(report.LongestWinStreak)],
            ["First run", Format.Date(report.FirstRun!.Value)],
            ["Last run", Format.Date(report.LastRun!.Value)],
        ];

        List<IReadOnlyList<string>> rolling =
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
                report.LossesByAct.Select(row => (IReadOnlyList<string>)[row.Label, Format.Count(row.Count)]).ToList()),
            new TableSection(
                "Top deaths",
                [new TableColumn("encounter"), new TableColumn("losses", RightAligned: true)],
                report.TopDeaths.Select(row => (IReadOnlyList<string>)[row.Label, Format.Count(row.Count)]).ToList()),
        ];
    }

    /// <summary>
    /// Every way of cutting the archive into consecutive stretches: by month, by patch,
    /// and by fixed-size blocks. All four are the same shape, so all four graph.
    /// </summary>
    private static IReadOnlyList<TableSection> Splits(WinrateReport report)
    {
        if (report.IsEmpty)
            return [];

        return
        [
            PeriodSection("By month", report.Months, "month", withFloors: true),
            PeriodSection("By patch", report.Patches, "patch"),
            // A block of exactly ten runs needs no win% column: the record is the rate
            // with a zero after it.
            PeriodSection("10-run blocks", report.Blocks10, "block", withOwnRate: false),
            PeriodSection("50-run blocks", report.Blocks50, "block"),
        ];
    }

    private static TableSection PeriodSection(
        string title,
        IReadOnlyList<PeriodRow> periods,
        string labelHeader,
        bool withFloors = false,
        bool withOwnRate = true)
    {
        List<TableColumn> columns =
        [
            new(labelHeader),
            new("from", RightAligned: true),
            new("to", RightAligned: true),
            new("W-L", RightAligned: true),
        ];
        if (withOwnRate)
            columns.Add(new TableColumn("win%", RightAligned: true));
        columns.Add(new TableColumn("overall%", RightAligned: true));
        if (withFloors)
            columns.Add(new TableColumn("avg floors", RightAligned: true));

        var rows = periods.Select(period =>
        {
            List<string> cells =
            [
                period.Label,
                Format.ShortDate(period.From),
                Format.ShortDate(period.To),
                Format.WinLoss(period.Tally),
            ];
            if (withOwnRate)
                cells.Add(Format.WholePercent(period.Tally));
            cells.Add(Format.Percent(period.CumulativeWinRate));
            if (withFloors)
                cells.Add(Format.Average(period.AverageFloors));
            return (IReadOnlyList<string>)cells;
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
                new TableColumn("W-L", RightAligned: true),
                new TableColumn("win%", RightAligned: true),
                new TableColumn("W-L", RightAligned: true),
                new TableColumn("win%", RightAligned: true),
                // Ten runs needs no rate column, for the same reason the 10-run blocks
                // table does without one.
                new TableColumn("W-L", RightAligned: true),
            ],
            report.Characters.Select(row => (IReadOnlyList<string>)
            [
                row.Character,
                Format.Count(row.All.Runs),
                Format.WinLoss(row.All),
                Format.WholePercent(row.All),
                Format.WinLoss(row.Last50),
                Format.WholePercent(row.Last50),
                Format.WinLoss(row.Last10),
            ]).ToList())
        {
            GroupHeaders = ["", "", "all time", "", "last 50", "", "last 10"],
        };

        // Each character gets one name over a record column and a rate column.
        var columns = new List<TableColumn> { new("month") };
        var groups = new List<string> { "" };
        foreach (var character in report.MatrixCharacters)
        {
            columns.Add(new TableColumn("W-L", RightAligned: true));
            columns.Add(new TableColumn("win%", RightAligned: true));
            groups.Add(character);
            groups.Add("");
        }

        var matrix = new TableSection(
            "Month by character",
            columns,
            report.MonthByCharacter.Select(row =>
            {
                var cells = new List<string> { row.Label };
                foreach (var tally in row.Cells)
                {
                    cells.Add(tally is { } played ? Format.WinLoss(played) : Format.Empty);
                    cells.Add(tally is { } rate ? Format.WholePercent(rate) : Format.Empty);
                }
                return (IReadOnlyList<string>)cells;
            }).ToList())
        {
            GroupHeaders = groups,
        };

        return [byCharacter, matrix];
    }
}

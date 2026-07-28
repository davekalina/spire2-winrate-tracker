namespace WinrateTracker.WinrateTrackerCode;

/// <summary>A column heading, and whether its values are numbers that line up on the right.</summary>
internal sealed record TableColumn(string Header, bool RightAligned = false);

/// <summary>A titled block of rows. One or more of these makes a tab.</summary>
internal sealed record TableSection(
    string Title,
    IReadOnlyList<TableColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string? Note = null)
{
    public bool IsEmpty => Rows.Count == 0;
}

/// <summary>The four tabs, in the order they appear.</summary>
internal enum ReportTab
{
    Overview,
    Blocks,
    Characters,
    Months,
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
        ReportTab.Blocks => "Blocks",
        ReportTab.Characters => "Characters",
        ReportTab.Months => "Months",
        _ => tab.ToString(),
    };

    public static IReadOnlyList<TableSection> Build(ReportTab tab, WinrateReport report) => tab switch
    {
        ReportTab.Overview => Overview(report),
        ReportTab.Blocks => Blocks(report),
        ReportTab.Characters => Characters(report),
        ReportTab.Months => Months(report),
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
                rolling,
                "A moving window over the most recent runs, so the newest form is not diluted by the whole archive."),
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

    private static IReadOnlyList<TableSection> Blocks(WinrateReport report)
    {
        if (report.IsEmpty)
            return [];

        return
        [
            BlockSection("10-run blocks", report.Blocks10,
                "Blocks are counted from the oldest run forward, so a block always covers the same ten runs. "
                + "The last column is the all-time win rate as of the end of that block."),
            BlockSection("50-run blocks", report.Blocks50, null),
        ];
    }

    private static TableSection BlockSection(string title, IReadOnlyList<BlockRow> blocks, string? note) =>
        new(
            title,
            [
                new TableColumn("block"),
                new TableColumn("from", RightAligned: true),
                new TableColumn("to", RightAligned: true),
                new TableColumn("W-L", RightAligned: true),
                new TableColumn("win%", RightAligned: true),
                new TableColumn("overall%", RightAligned: true),
            ],
            blocks.Select(block => (IReadOnlyList<string>)
            [
                block.Label,
                Format.ShortDate(block.From),
                Format.ShortDate(block.To),
                Format.WinLoss(block.Block),
                Format.Percent(block.Block),
                Format.Percent(block.CumulativeWinRate),
            ]).ToList(),
            note);

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
                new TableColumn("last 10", RightAligned: true),
                new TableColumn("last 10%", RightAligned: true),
                new TableColumn("avg act", RightAligned: true),
            ],
            report.Characters.Select(row => (IReadOnlyList<string>)
            [
                row.Character,
                Format.Count(row.All.Runs),
                Format.WinLoss(row.All),
                Format.Percent(row.All),
                Format.WinLoss(row.Recent),
                Format.Percent(row.Recent),
                Format.AverageAct(row.AverageAct),
            ]).ToList(),
            "Best win rate first. \"last 10\" is that character's own ten most recent runs.");

        var columns = new List<TableColumn> { new("character") };
        columns.AddRange(report.MatrixMonths.Select(month => new TableColumn(Format.MonthAbbreviation(month), RightAligned: true)));

        var matrix = new TableSection(
            "Character by month",
            columns,
            report.CharacterByMonth
                .Select(row => (IReadOnlyList<string>)new[] { row.Label }.Concat(row.Cells).ToList())
                .ToList(),
            "Wins out of runs.");

        return [byCharacter, matrix];
    }

    private static IReadOnlyList<TableSection> Months(WinrateReport report)
    {
        if (report.IsEmpty)
            return [];

        return
        [
            new TableSection(
                "By month",
                [
                    new TableColumn("month"),
                    new TableColumn("runs", RightAligned: true),
                    new TableColumn("W-L", RightAligned: true),
                    new TableColumn("win%", RightAligned: true),
                    new TableColumn("avg floors", RightAligned: true),
                    new TableColumn("avg act", RightAligned: true),
                    new TableColumn("avg elites", RightAligned: true),
                    new TableColumn("avg min", RightAligned: true),
                ],
                report.Months.Select(row => (IReadOnlyList<string>)
                [
                    Format.MonthName(row.Month),
                    Format.Count(row.Tally.Runs),
                    Format.WinLoss(row.Tally),
                    Format.Percent(row.Tally),
                    Format.Average(row.AverageNodes),
                    Format.AverageAct(row.AverageAct),
                    Format.Average(row.AverageElites),
                    Format.Minutes(row.AverageMinutes),
                ]).ToList(),
                "\"avg act\" counts a win as act 4, so it separates finishing act 3 from dying in it."),
        ];
    }
}

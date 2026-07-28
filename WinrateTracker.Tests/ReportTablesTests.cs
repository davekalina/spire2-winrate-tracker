using WinrateTracker.WinrateTrackerCode;
using Xunit;
using static WinrateTracker.Tests.TestRuns;

namespace WinrateTracker.Tests;

public class ReportTablesTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    private static WinrateReport Report(string results) =>
        WinrateReport.Build(Sequence(results, Start));

    private static TableSection Section(ReportTab tab, WinrateReport report, string title) =>
        Assert.Single(ReportTables.Build(tab, report), section => section.Title == title);

    /// <summary>Every tab, so a new one cannot be added without these checks covering it.</summary>
    private static readonly ReportTab[] AllTabs = Enum.GetValues<ReportTab>();

    [Fact]
    public void Every_tab_has_a_title()
    {
        Assert.Equal(
            ["Overview", "Blocks", "Characters", "Months"],
            AllTabs.Select(ReportTables.Title));
    }

    [Fact]
    public void An_empty_report_builds_no_sections_on_any_tab()
    {
        var empty = WinrateReport.Build([]);

        Assert.All(AllTabs, tab => Assert.Empty(ReportTables.Build(tab, empty)));
    }

    [Fact]
    public void Every_row_has_one_cell_per_column()
    {
        var report = Report("WLLWLWLLLWLLWLLLWLLWLLLWWLWL");

        foreach (var tab in AllTabs)
        foreach (var section in ReportTables.Build(tab, report))
            Assert.All(section.Rows, row => Assert.Equal(section.Columns.Count, row.Count));
    }

    [Fact]
    public void No_tab_builds_an_empty_section()
    {
        var report = Report("WLLWLWLLLWLLWLLLWLLWLLLWWLWL");

        foreach (var tab in AllTabs)
            Assert.All(ReportTables.Build(tab, report), section => Assert.False(section.IsEmpty, section.Title));
    }

    [Fact]
    public void Overall_leads_with_the_record_and_the_streaks()
    {
        var rows = Section(ReportTab.Overview, Report("WLLWW"), "Overall").Rows;

        Assert.Equal(["Runs", "5"], rows[0]);
        Assert.Equal(["Record", "3-2"], rows[1]);
        Assert.Equal(["Win rate", "60.0%"], rows[2]);
        Assert.Equal(["Current streak", "2 wins"], rows[3]);
        Assert.Equal(["Longest win streak", "2"], rows[4]);
        Assert.Equal(["First run", "2026-01-01"], rows[5]);
        Assert.Equal(["Last run", "2026-01-05"], rows[6]);
    }

    [Fact]
    public void Rolling_win_rate_starts_at_all_time_then_narrows()
    {
        var rows = Section(ReportTab.Overview, Report(new string('L', 100) + new string('W', 10)), "Rolling win rate").Rows;

        Assert.Equal(["All time", "10-100", "9.1%"], rows[0]);
        Assert.Equal(["Last 100", "10-90", "10.0%"], rows[1]);
        Assert.Equal(["Last 50", "10-40", "20.0%"], rows[2]);
        Assert.Equal(["Last 25", "10-15", "40.0%"], rows[3]);
        Assert.Equal(["Last 10", "10-0", "100.0%"], rows[4]);
    }

    [Fact]
    public void Rolling_win_rate_is_all_time_alone_when_the_archive_is_short()
    {
        var rows = Section(ReportTab.Overview, Report("WL"), "Rolling win rate").Rows;

        Assert.Equal(["All time", "1-1", "50.0%"], Assert.Single(rows));
    }

    [Fact]
    public void Block_rows_end_with_the_running_all_time_rate()
    {
        var section = Section(ReportTab.Blocks, Report(new string('L', 10) + new string('W', 10)), "10-run blocks");

        Assert.Equal(
            ["block", "from", "to", "W-L", "win%", "overall%"],
            section.Columns.Select(column => column.Header));
        Assert.Equal(["11-20", "01-11", "01-20", "10-0", "100.0%", "50.0%"], section.Rows[0]);
        Assert.Equal(["1-10", "01-01", "01-10", "0-10", "0.0%", "0.0%"], section.Rows[1]);
    }

    [Fact]
    public void Both_block_tables_use_the_same_columns()
    {
        var sections = ReportTables.Build(ReportTab.Blocks, Report(new string('W', 60)));

        Assert.Equal(2, sections.Count);
        Assert.Equal(
            sections[0].Columns.Select(column => column.Header),
            sections[1].Columns.Select(column => column.Header));
    }

    [Fact]
    public void The_character_matrix_gains_a_column_per_month()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 5), win: true, character: "Ironclad"),
            Run(Unix(2026, 2, 5), character: "Ironclad"),
        };

        var section = Section(ReportTab.Characters, WinrateReport.Build(runs), "Character by month");

        Assert.Equal(["character", "Jan", "Feb"], section.Columns.Select(column => column.Header));
        Assert.Equal(["Ironclad", "1/1", "0/1"], section.Rows[0]);
        Assert.Equal(["Total", "1/1", "0/1"], section.Rows[1]);
        Assert.Equal(["Total %", "100.0%", "0.0%"], section.Rows[2]);
    }

    [Fact]
    public void Months_are_named_rather_than_numbered()
    {
        var section = Section(ReportTab.Months, Report("WL"), "By month");

        Assert.Equal("Jan 2026", section.Rows[0][0]);
    }

    [Fact]
    public void Numeric_columns_are_marked_for_right_alignment_and_labels_are_not()
    {
        var section = Section(ReportTab.Characters, Report("WL"), "By character");

        Assert.False(section.Columns[0].RightAligned);
        Assert.All(section.Columns.Skip(1), column => Assert.True(column.RightAligned));
    }

    [Fact]
    public void Tables_that_need_a_caveat_carry_one()
    {
        Assert.NotNull(Section(ReportTab.Blocks, Report("WL"), "10-run blocks").Note);
        Assert.NotNull(Section(ReportTab.Overview, Report("WL"), "Rolling win rate").Note);
        Assert.NotNull(Section(ReportTab.Characters, Report("WL"), "By character").Note);
        Assert.NotNull(Section(ReportTab.Months, Report("WL"), "By month").Note);
    }
}

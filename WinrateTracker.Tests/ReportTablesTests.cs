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
            ["Overview", "Splits", "Characters"],
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
        var section = Section(ReportTab.Splits, Report(new string('L', 10) + new string('W', 10)), "10-run blocks");

        // A block of ten needs no win% column of its own.
        Assert.Equal(
            ["block", "from", "to", "W-L", "overall%"],
            section.Columns.Select(column => column.Header));
        Assert.Equal(["11-20", "01-11", "01-20", "10-0", "50.0%"], section.Rows[0]);
        Assert.Equal(["1-10", "01-01", "01-10", "0-10", "0.0%"], section.Rows[1]);
    }

    [Fact]
    public void Fifty_run_blocks_carry_their_own_rate_where_ten_run_blocks_do_not()
    {
        var sections = ReportTables.Build(ReportTab.Splits, Report(new string('W', 60)));

        var ten = Assert.Single(sections, section => section.Title == "10-run blocks");
        var fifty = Assert.Single(sections, section => section.Title == "50-run blocks");

        Assert.DoesNotContain("win%", ten.Columns.Select(column => column.Header));
        Assert.Contains("win%", fifty.Columns.Select(column => column.Header));
    }

    [Fact]
    public void Records_and_rates_live_in_separate_columns()
    {
        var section = Section(ReportTab.Splits, Report(new string('W', 60)), "By patch");

        Assert.Equal(
            ["patch", "from", "to", "W-L", "win%", "overall%"],
            section.Columns.Select(column => column.Header));
        // No bracketed rate stowed inside the record cell.
        Assert.All(section.Rows, row => Assert.DoesNotContain("(", row[3]));
    }

    [Fact]
    public void Months_lead_the_splits_tab_and_are_named_rather_than_numbered()
    {
        var sections = ReportTables.Build(ReportTab.Splits, Report("WL"));

        Assert.Equal(
            ["By month", "By patch", "10-run blocks", "50-run blocks"],
            sections.Select(section => section.Title));
        Assert.Equal("Jan 2026", sections[0].Rows[0][0]);
    }

    [Fact]
    public void Every_period_table_carries_a_series_matching_its_rows()
    {
        var report = Report(new string('L', 10) + new string('W', 10));

        foreach (var section in ReportTables.Build(ReportTab.Splits, report))
        {
            Assert.NotNull(section.Series);
            Assert.Equal(section.Rows.Count, section.Series!.Count);
        }
    }

    [Fact]
    public void A_series_reads_oldest_first_even_though_the_table_reads_newest_first()
    {
        var section = Section(ReportTab.Splits, Report(new string('L', 10) + new string('W', 10)), "10-run blocks");

        Assert.True(section.IsGraphable);
        Assert.Equal("11-20", section.Rows[0][0]);
        Assert.Equal("1-10", section.Series![0].Label);
        Assert.Equal(0, section.Series[0].Wins);
        Assert.Equal(10, section.Series[1].Wins);
    }

    [Fact]
    public void A_single_period_is_not_worth_a_graph()
    {
        var section = Section(ReportTab.Splits, Report("WL"), "10-run blocks");

        Assert.Single(section.Series!);
        Assert.False(section.IsGraphable);
    }

    [Fact]
    public void Tables_that_cannot_be_plotted_carry_no_series()
    {
        Assert.All(
            ReportTables.Build(ReportTab.Characters, Report("WLWL")),
            section => Assert.Null(section.Series));
    }

    [Fact]
    public void Numeric_columns_are_marked_for_right_alignment_and_labels_are_not()
    {
        var section = Section(ReportTab.Characters, Report("WL"), "By character");

        Assert.False(section.Columns[0].RightAligned);
        Assert.All(section.Columns.Skip(1), column => Assert.True(column.RightAligned));
    }

    [Fact]
    public void The_character_matrix_runs_months_down_and_characters_across()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 5), win: true, character: "Ironclad"),
            Run(Unix(2026, 2, 5), character: "Silent"),
        };

        var section = Section(ReportTab.Characters, WinrateReport.Build(runs), "Month by character");

        // One name per character, over a record column and a rate column.
        Assert.Equal(["", "Ironclad", "", "Silent", ""], section.GroupHeaders);
        Assert.Equal(["month", "W-L", "win%", "W-L", "win%"], section.Columns.Select(column => column.Header));
        Assert.Equal(["Feb 2026", "—", "—", "0-1", "0%"], section.Rows[0]);
        Assert.Equal(["Jan 2026", "1-0", "100%", "—", "—"], section.Rows[1]);
    }

    [Fact]
    public void The_character_table_splits_each_record_from_its_rate()
    {
        var section = Section(ReportTab.Characters, Report("WLWL"), "By character");

        Assert.Equal(["", "", "all time", "", "last 50", "", "last 10"], section.GroupHeaders);
        Assert.Equal(
            ["character", "runs", "W-L", "win%", "W-L", "win%", "W-L"],
            section.Columns.Select(column => column.Header));
        Assert.Equal(["Ironclad", "4", "2-2", "50%", "2-2", "50%", "2-2"], section.Rows[0]);
    }

    [Fact]
    public void A_group_header_row_has_one_entry_per_column()
    {
        var report = Report("WLLWLWLLLWLLWLLLWLLWLLLWWLWL");

        foreach (var tab in AllTabs)
        foreach (var section in ReportTables.Build(tab, report))
            if (section.GroupHeaders is { } groups)
                Assert.Equal(section.Columns.Count, groups.Count);
    }
}

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

    /// <summary>A row as plain strings, a paired cell joined by a space.</summary>
    private static string[] Texts(IReadOnlyList<TableCell> row) =>
        row.Select(cell => cell.Text).ToArray();

    /// <summary>Every tab, so a new one cannot be added without these checks covering it.</summary>
    private static readonly ReportTab[] AllTabs = Enum.GetValues<ReportTab>();

    [Fact]
    public void Every_tab_has_a_title()
    {
        Assert.Equal(
            ["Overview", "Splits", "Characters", "Cards", "Relics"],
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

        Assert.Equal(["Runs", "5"], Texts(rows[0]));
        Assert.Equal(["Record", "3-2"], Texts(rows[1]));
        Assert.Equal(["Win rate", "60.0%"], Texts(rows[2]));
        Assert.Equal(["Current streak", "2 wins"], Texts(rows[3]));
        Assert.Equal(["Longest win streak", "2"], Texts(rows[4]));
        Assert.Equal(["First run", "2026-01-01"], Texts(rows[5]));
        Assert.Equal(["Last run", "2026-01-05"], Texts(rows[6]));
    }

    [Fact]
    public void Rolling_win_rate_starts_at_all_time_then_narrows()
    {
        var rows = Section(ReportTab.Overview, Report(new string('L', 100) + new string('W', 10)), "Rolling win rate").Rows;

        Assert.Equal(["All time", "10-100", "9.1%"], Texts(rows[0]));
        Assert.Equal(["Last 100", "10-90", "10.0%"], Texts(rows[1]));
        Assert.Equal(["Last 50", "10-40", "20.0%"], Texts(rows[2]));
        Assert.Equal(["Last 25", "10-15", "40.0%"], Texts(rows[3]));
        Assert.Equal(["Last 10", "10-0", "100.0%"], Texts(rows[4]));
    }

    [Fact]
    public void Rolling_win_rate_is_all_time_alone_when_the_archive_is_short()
    {
        var rows = Section(ReportTab.Overview, Report("WL"), "Rolling win rate").Rows;

        Assert.Equal(["All time", "1-1", "50.0%"], Texts(Assert.Single(rows)));
    }

    [Fact]
    public void Block_rows_end_with_the_running_all_time_rate()
    {
        var section = Section(ReportTab.Splits, Report(new string('L', 10) + new string('W', 10)), "10-run blocks");

        // A block of ten needs no rate of its own.
        Assert.Equal(
            ["block", "from", "to", "record", "cumulative%"],
            section.Columns.Select(column => column.Header));
        Assert.Equal(["11-20", "01-11", "01-20", "10-0", "50.0%"], Texts(section.Rows[0]));
        Assert.Equal(["1-10", "01-01", "01-10", "0-10", "0.0%"], Texts(section.Rows[1]));
        Assert.All(section.Rows, row => Assert.Single(row[3].Parts));
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
    public void A_period_keeps_its_record_and_its_rate_in_columns_of_their_own()
    {
        var section = Section(ReportTab.Splits, Report(new string('W', 60)), "By patch");

        Assert.Equal(
            ["patch", "from", "to", "record", "win%", "cumulative%"],
            section.Columns.Select(column => column.Header));
        Assert.Equal("60-0", section.Rows[0][3].Text);
        Assert.Equal("100%", section.Rows[0][4].Text);
    }

    [Fact]
    public void A_month_is_its_own_date_and_carries_no_from_or_to()
    {
        var section = Section(ReportTab.Splits, Report("WL"), "By month");

        Assert.Equal(
            ["month", "record", "win%", "cumulative%", "avg floors"],
            section.Columns.Select(column => column.Header));
        Assert.Equal("Jan 2026", section.Rows[0][0].Text);
        Assert.Equal("1-1", section.Rows[0][1].Text);
    }

    /// <summary>
    /// The Characters tab still pairs a record with its rate inside one column. It has one
    /// column per character, so splitting them there is what made the table too wide — the
    /// reason the paired cell exists at all.
    /// </summary>
    [Fact]
    public void A_character_keeps_its_record_and_rate_paired_in_one_column()
    {
        var section = Section(ReportTab.Characters, Report(new string('W', 60)), "By character");

        Assert.Equal(["60-0", "100%"], section.Rows[0][2].Parts);
    }

    [Fact]
    public void Months_lead_the_splits_tab_and_are_named_rather_than_numbered()
    {
        var sections = ReportTables.Build(ReportTab.Splits, Report("WL"));

        Assert.Equal(
            ["By month", "By patch", "10-run blocks", "50-run blocks", "By time of day", "Every 4 hours"],
            sections.Select(section => section.Title));
        Assert.Equal("Jan 2026", sections[0].Rows[0][0].Text);
    }

    [Fact]
    public void Every_period_table_carries_a_series_matching_its_rows()
    {
        var report = Report(new string('L', 10) + new string('W', 10));
        var sections = ReportTables.Build(ReportTab.Splits, report);

        // The four consecutive-stretch tables graph. The time-of-day tables are buckets,
        // not a run of time, so they carry no series and must not claim to be graphable.
        foreach (var title in new[] { "By month", "By patch", "10-run blocks", "50-run blocks" })
        {
            var section = Assert.Single(sections, candidate => candidate.Title == title);
            Assert.NotNull(section.Series);
            Assert.Equal(section.Rows.Count, section.Series!.Count);
        }

        foreach (var title in new[] { "By time of day", "Every 4 hours" })
            Assert.False(Assert.Single(sections, candidate => candidate.Title == title).IsGraphable);
    }

    [Fact]
    public void A_series_reads_oldest_first_even_though_the_table_reads_newest_first()
    {
        var section = Section(ReportTab.Splits, Report(new string('L', 10) + new string('W', 10)), "10-run blocks");

        Assert.True(section.IsGraphable);
        Assert.Equal("11-20", section.Rows[0][0].Text);
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

        var section = Section(ReportTab.Characters, WinrateReport.Build(runs), "Character by Month");

        // One column per character, its record and rate paired inside it.
        Assert.Equal(["month", "Ironclad", "Silent"], section.Columns.Select(column => column.Header));
        Assert.Equal(["Jan 2026", "Feb 2026", "Total"], section.Rows.Select(row => row[0].Text));
        Assert.Equal(["1-0", "100%"], section.Rows[0][1].Parts);
        Assert.Equal(["0-1", "0%"], section.Rows[1][2].Parts);
        // A month a character did not play reads as a dash with no rate beside it.
        Assert.Equal(["—", ""], section.Rows[1][1].Parts);
    }

    [Fact]
    public void The_character_table_pairs_each_record_with_its_rate()
    {
        var section = Section(ReportTab.Characters, Report("WLWL"), "By character");

        Assert.Equal(
            ["character", "runs", "all time", "last 50", "last 10"],
            section.Columns.Select(column => column.Header));
        Assert.Equal(["2-2", "50%"], section.Rows[0][2].Parts);
        Assert.Equal(["2-2", "50%"], section.Rows[0][3].Parts);
        // Ten runs needs no rate.
        Assert.Equal(["2-2"], section.Rows[0][4].Parts);
    }

    [Fact]
    public void A_paired_cell_never_carries_more_than_two_parts()
    {
        var report = Report("WLLWLWLLLWLLWLLLWLLWLLLWWLWL");

        foreach (var tab in AllTabs)
        foreach (var section in ReportTables.Build(tab, report))
        foreach (var row in section.Rows)
            Assert.All(row, cell => Assert.InRange(cell.Parts.Count, 1, 2));
    }

    [Fact]
    public void Time_of_day_lists_every_part_of_the_day_even_when_one_is_unplayed()
    {
        // TestRuns starts every run at noon, so all of them are afternoon runs.
        var rows = Section(ReportTab.Splits, Report("WLW"), "By time of day").Rows;

        Assert.Equal(["Morning", "Afternoon", "Night"], rows.Select(row => row[0].Text));
        Assert.Equal(["Afternoon", "3", "2-1", "66.7%"], Texts(rows[1]));
        Assert.Equal("0", rows[0][1].Text);
    }

    [Fact]
    public void Four_hour_blocks_cover_the_whole_day_in_six_rows()
    {
        var rows = Section(ReportTab.Splits, Report("WL"), "Every 4 hours").Rows;

        Assert.Equal(6, rows.Count);
        Assert.Equal("00:00-03:59", rows[0][0].Text);
        Assert.Equal("20:00-23:59", rows[5][0].Text);
        // Noon runs land in the 12:00 block.
        Assert.Equal(["12:00-15:59", "2", "1-1", "50.0%"], Texts(rows[3]));
    }

    [Fact]
    public void Picks_lead_with_the_best_card_and_show_what_backs_it_up()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 1), win: true, cards: ["SHIV"], relics: ["KUNAI"]),
            Run(Unix(2026, 1, 2), win: true, cards: ["SHIV", "CLASH"]),
            Run(Unix(2026, 1, 3), cards: ["CLASH"]),
        };

        var report = WinrateReport.Build(runs);
        var cards = Assert.Single(ReportTables.Build(ReportTab.Cards, report));
        var relics = Assert.Single(ReportTables.Build(ReportTab.Relics, report));

        // No heading: the tab is already named for the table.
        Assert.Equal("", cards.Title);
        // Shiv 2-0 outranks Clash 1-1, and the pick count sits beside the rate. Rarity is
        // unknown here: it comes from the game's models, which no test loads.
        Assert.Equal(["Shiv", "—", "2", "2-0", "100.0%"], Texts(cards.Rows[0]));
        Assert.Equal(["Clash", "—", "2", "1-1", "50.0%"], Texts(cards.Rows[1]));
        Assert.Equal(["Kunai", "—", "1", "1-0", "100.0%"], Texts(relics.Rows[0]));
    }

    [Fact]
    public void The_minimum_and_the_rarity_narrow_only_the_tab_they_belong_to()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 1), win: true, cards: ["SHIV"], relics: ["KUNAI"]),
            Run(Unix(2026, 1, 2), win: true, cards: ["SHIV"]),
            Run(Unix(2026, 1, 3), cards: ["CLASH"]),
        };
        var report = WinrateReport.Build(runs);

        var cards = Assert.Single(ReportTables.Build(
            ReportTab.Cards, report, new PickFilter { MinimumPicks = 2 }));

        // Clash was picked once and drops out; the runs behind Shiv are untouched by it.
        Assert.Equal("Shiv", Assert.Single(cards.Rows)[0].Text);
        Assert.Equal("2-0", cards.Rows[0][3].Text);
    }

    [Fact]
    public void A_pick_tab_with_nothing_left_after_filtering_builds_no_table()
    {
        var runs = new List<RunRecord> { Run(Unix(2026, 1, 1), win: true, cards: ["SHIV"]) };

        Assert.Empty(ReportTables.Build(
            ReportTab.Cards, WinrateReport.Build(runs), new PickFilter { MinimumPicks = 5 }));
    }

    [Fact]
    public void Runs_with_nothing_picked_build_no_pick_tables_at_all()
    {
        Assert.Empty(ReportTables.Build(ReportTab.Cards, Report("WLW")));
        Assert.Empty(ReportTables.Build(ReportTab.Relics, Report("WLW")));
    }
}

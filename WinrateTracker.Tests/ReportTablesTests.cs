using WinrateTracker.WinrateTrackerCode;
using Xunit;
using static WinrateTracker.Tests.TestRuns;

namespace WinrateTracker.Tests;

public class ReportTablesTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    private static WinrateReport Report(string results) =>
        WinrateReport.Build(Sequence(results, Start));

    /// <summary>
    /// Every section on a tab, including the ones paired side by side. A section that shares
    /// a row with another is still a table and must satisfy everything the others do.
    /// </summary>
    private static List<TableSection> Sections(ReportTab tab, WinrateReport report, PickFilter? picks = null) =>
        ReportTables.Build(tab, report, picks)
            .SelectMany(section => section.Beside is null ? [section] : new[] { section, section.Beside })
            .ToList();

    private static TableSection Section(ReportTab tab, WinrateReport report, string title) =>
        Assert.Single(Sections(tab, report), section => section.Title == title);

    /// <summary>A row as plain strings, a paired cell joined by a space.</summary>
    private static string[] Texts(IReadOnlyList<TableCell> row) =>
        row.Select(cell => cell.Text).ToArray();

    private static string[] Headers(TableSection section) =>
        section.Columns.Select(column => column.Header).ToArray();

    /// <summary>Every tab, so a new one cannot be added without these checks covering it.</summary>
    private static readonly ReportTab[] AllTabs = Enum.GetValues<ReportTab>();

    [Fact]
    public void Every_tab_has_a_title()
    {
        Assert.Equal(
            ["Home", "Splits", "Characters", "Cards", "Relics"],
            AllTabs.Select(ReportTables.Title));
    }

    /// <summary>
    /// Home is not a table. It is built by <see cref="HomePanel" /> instead, so asking this
    /// file for it has to come back empty rather than with a half-built section.
    /// </summary>
    [Fact]
    public void The_home_tab_builds_no_table_sections()
    {
        Assert.Empty(ReportTables.Build(ReportTab.Home, Report("WLWLW")));
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
        foreach (var section in Sections(tab, report))
            Assert.All(section.Rows, row => Assert.Equal(section.Columns.Count, row.Count));
    }

    [Fact]
    public void No_tab_builds_an_empty_section()
    {
        var report = Report("WLLWLWLLLWLLWLLLWLLWLLLWWLWL");

        foreach (var tab in AllTabs)
            Assert.All(Sections(tab, report), section => Assert.False(section.IsEmpty, section.Title));
    }

    // ── splits ───────────────────────────────────────────────────────────────

    [Fact]
    public void Splits_cuts_the_archive_four_ways_with_the_two_clock_tables_paired()
    {
        var sections = ReportTables.Build(ReportTab.Splits, Report("WL"));

        Assert.Equal(
            ["By month", "By patch", "50-run blocks", "By time of day"],
            sections.Select(section => section.Title));
        // The two time-of-day tables are narrow and about one question, so they share a row.
        Assert.Equal("Every 4 hours", sections[^1].Beside?.Title);
        Assert.All(sections.Take(3), section => Assert.Null(section.Beside));
        Assert.Equal("Jan 2026", sections[0].Rows[0][0].Text);
    }

    /// <summary>
    /// 10-run blocks are gone: the Home trend covers that granularity, and a second table
    /// saying the same thing at more length is a table nobody reads.
    /// </summary>
    [Fact]
    public void Ten_run_blocks_are_no_longer_a_table()
    {
        Assert.DoesNotContain(
            Sections(ReportTab.Splits, Report(new string('W', 60))),
            section => section.Title.Contains("10-run"));
    }

    [Fact]
    public void Block_rows_end_with_the_running_all_time_rate()
    {
        var section = Section(ReportTab.Splits, Report(new string('L', 50) + new string('W', 50)), "50-run blocks");

        Assert.Equal(
            ["block", "from", "to", "record", "win%", "vs your avg 50.0%", "cumulative%"],
            Headers(section));
        Assert.Equal(["51-100", "02-20", "04-10", "50-0", "100%", "", "50.0%"], Texts(section.Rows[0]));
        Assert.Equal(["1-50", "01-01", "02-19", "0-50", "0%", "", "0.0%"], Texts(section.Rows[1]));
    }

    [Fact]
    public void A_period_keeps_its_record_and_its_rate_in_columns_of_their_own()
    {
        var section = Section(ReportTab.Splits, Report(new string('W', 60)), "By patch");

        Assert.Equal(
            ["patch", "from", "to", "record", "win%", "vs your avg 100.0%", "cumulative%"],
            Headers(section));
        Assert.Equal("60-0", section.Rows[0][3].Text);
        Assert.Equal("100%", section.Rows[0][4].Text);
    }

    [Fact]
    public void A_month_is_its_own_date_and_carries_no_from_or_to()
    {
        var section = Section(ReportTab.Splits, Report("WL"), "By month");

        Assert.Equal(
            ["month", "record", "win%", "vs your avg 50.0%", "cumulative%", "avg floors"],
            Headers(section));
        Assert.Equal("Jan 2026", section.Rows[0][0].Text);
        Assert.Equal("1-1", section.Rows[0][1].Text);
    }

    // ── comparison bars ──────────────────────────────────────────────────────

    /// <summary>
    /// The bar column draws rather than writes, so its cells carry a value and no text. The
    /// notch is the player's own rate, and the track ends at 40% — fixed, so two visits to
    /// the same table agree about how good a month was.
    /// </summary>
    [Fact]
    public void A_period_bar_measures_the_period_against_the_players_own_rate()
    {
        // One month, one win in four, so the month's own rate is also the all-time rate.
        var section = Section(ReportTab.Splits, Report("WLLL"), "By month");
        var bar = section.Columns[3];

        Assert.NotNull(bar.Bar);
        Assert.Equal(0.25d, bar.Bar!.Baseline, 6);
        Assert.Equal(0.40d, bar.Bar.Scale, 6);
        Assert.False(bar.Bar.Signed);
        Assert.NotNull(bar.Tooltip);
        Assert.Empty(section.Rows[0][3].Parts);
        Assert.Equal(0.25d, section.Rows[0][3].Bar!.Value, 6);
    }

    /// <summary>
    /// A pick's bar runs both ways from that rate instead, because what matters about a
    /// card is which side of your average it falls on and by how far.
    /// </summary>
    [Fact]
    public void A_pick_bar_is_signed_against_the_players_own_rate()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 1), win: true, cards: ["SHIV"]),
            Run(Unix(2026, 1, 2), cards: ["CLASH"]),
        };

        var section = Assert.Single(ReportTables.Build(ReportTab.Cards, WinrateReport.Build(runs)));
        var bar = section.Columns[^1];

        Assert.Equal("vs your avg 50.0%", bar.Header);
        Assert.True(bar.Bar!.Signed);
        Assert.Equal(0.5d, bar.Bar.Baseline, 6);
        // The whole 0-100% range, so the notch sits where the rate actually is.
        Assert.Equal(1.0d, bar.Bar.Scale, 6);
        Assert.Equal(1.0d, section.Rows[0][^1].Bar!.Value, 6);
        Assert.Equal(0.0d, section.Rows[1][^1].Bar!.Value, 6);
    }

    // ── series ───────────────────────────────────────────────────────────────

    [Fact]
    public void Every_period_table_carries_a_series_matching_its_rows()
    {
        var report = Report(new string('L', 50) + new string('W', 50));
        var sections = Sections(ReportTab.Splits, report);

        // The three consecutive-stretch tables graph. The time-of-day tables are buckets,
        // not a run of time, so they carry no series and must not claim to be graphable.
        foreach (var title in new[] { "By month", "By patch", "50-run blocks" })
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
        var section = Section(ReportTab.Splits, Report(new string('L', 50) + new string('W', 50)), "50-run blocks");

        Assert.True(section.IsGraphable);
        Assert.Equal("51-100", section.Rows[0][0].Text);
        Assert.Equal("1-50", section.Series![0].Label);
        Assert.Equal(0, section.Series[0].Wins);
        Assert.Equal(50, section.Series[1].Wins);
    }

    [Fact]
    public void A_single_period_is_not_worth_a_graph()
    {
        var section = Section(ReportTab.Splits, Report("WL"), "By month");

        Assert.Single(section.Series!);
        Assert.False(section.IsGraphable);
    }

    [Fact]
    public void Tables_that_cannot_be_plotted_carry_no_series()
    {
        Assert.All(
            Sections(ReportTab.Characters, Report("WLWL")),
            section => Assert.Null(section.Series));
    }

    // ── characters ───────────────────────────────────────────────────────────

    [Fact]
    public void Numeric_columns_are_marked_for_right_alignment_and_labels_are_not()
    {
        var section = Section(ReportTab.Characters, Report("WL"), "By character");

        // character, then three number columns, then the bar and the pips — which draw
        // rather than write, and so line up on the left like a label does.
        Assert.False(section.Columns[0].RightAligned);
        Assert.All(section.Columns.Skip(1).Take(3), column => Assert.True(column.RightAligned));
        Assert.All(section.Columns.Skip(4), column => Assert.False(column.RightAligned));
    }

    [Fact]
    public void The_character_table_pairs_each_record_with_its_rate()
    {
        var section = Section(ReportTab.Characters, Report("WLWL"), "By character");

        Assert.Equal(
            ["character", "runs", "all time", "last 50", "vs your avg 50.0%", "last 10"],
            Headers(section));
        Assert.Equal(["2-2", "50%"], section.Rows[0][2].Parts);
        Assert.Equal(["2-2", "50%"], section.Rows[0][3].Parts);
    }

    /// <summary>
    /// The last ten are pips rather than a record: four wins in a row and four scattered
    /// are the same <c>4-6</c> and a different thing to know.
    /// </summary>
    [Fact]
    public void The_character_table_shows_the_last_ten_runs_one_by_one_oldest_first()
    {
        var section = Section(ReportTab.Characters, Report("LLWWLWLLLW"), "By character");
        var pips = section.Rows[0][^1];

        Assert.Empty(pips.Parts);
        Assert.Equal([false, false, true, true, false, true, false, false, false, true], pips.Pips!);
    }

    [Fact]
    public void Character_rows_and_headings_ask_for_the_games_own_art()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 5), win: true, character: "Ironclad"),
            Run(Unix(2026, 2, 5), character: "Silent"),
        };
        var sections = Sections(ReportTab.Characters, WinrateReport.Build(runs));

        var table = Assert.Single(sections, section => section.Title == "By character");
        Assert.Equal("character/ironclad", Assert.Single(table.Rows, row => row[0].Text == "Ironclad")[0].Icon);

        var matrix = Assert.Single(sections, section => section.Title == "Character by month");
        Assert.Equal([null, "character/ironclad", "character/silent"], matrix.Columns.Select(column => column.Icon));
        Assert.All(sections, section => Assert.True(section.HasArt));
    }

    [Fact]
    public void The_character_matrix_runs_months_down_and_characters_across()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 5), win: true, character: "Ironclad"),
            Run(Unix(2026, 2, 5), character: "Silent"),
        };

        var section = Section(ReportTab.Characters, WinrateReport.Build(runs), "Character by month");

        // One column per character, its record and rate paired inside it.
        Assert.Equal(["month", "Ironclad", "Silent"], Headers(section));
        Assert.Equal(["Jan 2026", "Feb 2026", "Total"], section.Rows.Select(row => row[0].Text));
        Assert.Equal(["1-0", "100%"], section.Rows[0][1].Parts);
        Assert.Equal(["0-1", "0%"], section.Rows[1][2].Parts);
        // A month a character did not play reads as a dash with no rate beside it.
        Assert.Equal(["—", ""], section.Rows[1][1].Parts);
    }

    [Fact]
    public void A_written_cell_never_carries_more_than_two_parts()
    {
        var report = Report("WLLWLWLLLWLLWLLLWLLWLLLWWLWL");

        foreach (var tab in AllTabs)
        foreach (var section in Sections(tab, report))
        foreach (var row in section.Rows)
            // Zero parts is a cell that draws instead of writing: a bar or a pip strip.
            Assert.All(row, cell => Assert.InRange(cell.Parts.Count, 0, 2));
    }

    // ── time of day ──────────────────────────────────────────────────────────

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

    // ── picks ────────────────────────────────────────────────────────────────

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
        // unknown here: it comes from the game's models, which no test loads. The trailing
        // cell is the comparison bar, which draws rather than writes.
        Assert.Equal(["Shiv", "—", "2", "2-0", "100.0%", ""], Texts(cards.Rows[0]));
        Assert.Equal(["Clash", "—", "2", "1-1", "50.0%", ""], Texts(cards.Rows[1]));
        Assert.Equal(["Kunai", "—", "1", "1-0", "100.0%", ""], Texts(relics.Rows[0]));
    }

    /// <summary>
    /// An unknown rarity asks for no art. The renderer still reserves the slot, so a row the
    /// game has no icon for stays lined up with the rows that do.
    /// </summary>
    [Fact]
    public void A_rarity_the_game_cannot_name_asks_for_no_icon()
    {
        var runs = new List<RunRecord> { Run(Unix(2026, 1, 1), win: true, cards: ["SHIV"]) };

        var cards = Assert.Single(ReportTables.Build(ReportTab.Cards, WinrateReport.Build(runs)));

        Assert.Null(cards.Rows[0][0].Icon);
        Assert.Null(cards.Rows[0][1].Icon);
        Assert.False(cards.HasArt);
    }

    /// <summary>
    /// Where the rarity icon sits depends on what else the row has to show. A card has no
    /// art small enough to recognise, so the rarity icon leads its name. A relic brings its
    /// own art, which takes that place, and the rarity icon goes back beside the rarity word.
    /// </summary>
    [Fact]
    public void A_pick_row_puts_its_rarity_icon_where_the_row_has_room_for_it()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 1), win: true, cards: ["SHIV"], relics: ["KUNAI"]),
        };
        GameData.RarityLookup = (table, id) => table == GameData.Cards ? "Rare" : "Shop";

        try
        {
            var card = Assert.Single(ReportTables.Build(ReportTab.Cards, WinrateReport.Build(runs))).Rows[0];
            Assert.Equal("rarity/cards/rare", card[0].Icon);
            Assert.Equal("card/SHIV", card[0].Preview);
            Assert.Equal("Rare", card[1].Text);
            Assert.Null(card[1].Icon);

            var relic = Assert.Single(ReportTables.Build(ReportTab.Relics, WinrateReport.Build(runs))).Rows[0];
            Assert.Null(relic[0].Icon);
            Assert.Equal("relic/KUNAI", relic[0].Preview);
            Assert.Equal("Shop", relic[1].Text);
            Assert.Equal("rarity/relics/shop", relic[1].Icon);
        }
        finally
        {
            GameData.RarityLookup = null;
        }
    }

    /// <summary>Only the pick tabs name a single thing, so only they carry previews.</summary>
    [Fact]
    public void No_other_tab_carries_a_preview()
    {
        var report = Report("WLLWLWLLLWLLWLLLWLLWLLLWWLWL");

        foreach (var tab in AllTabs.Where(tab => tab is not (ReportTab.Cards or ReportTab.Relics)))
        foreach (var section in Sections(tab, report))
        foreach (var row in section.Rows)
            Assert.All(row, cell => Assert.Null(cell.Preview));
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

using WinrateTracker.WinrateTrackerCode;
using Xunit;
using static WinrateTracker.Tests.TestRuns;

namespace WinrateTracker.Tests;

public class WinrateReportTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    private static WinrateReport Report(string results) =>
        WinrateReport.Build(Sequence(results, Start));

    [Fact]
    public void Overall_counts_wins_and_losses()
    {
        var report = Report("WLLWL");

        Assert.Equal(5, report.Overall.Runs);
        Assert.Equal(2, report.Overall.Wins);
        Assert.Equal(3, report.Overall.Losses);
        Assert.Equal(0.4d, report.Overall.WinRate, 6);
    }

    [Fact]
    public void An_empty_archive_reports_empty_rather_than_dividing_by_zero()
    {
        var report = WinrateReport.Build([]);

        Assert.True(report.IsEmpty);
        Assert.Equal(0, report.Overall.Runs);
        Assert.Equal(0d, report.Overall.WinRate);
        Assert.Null(report.FirstRun);
        Assert.Empty(report.Blocks10);
        Assert.Empty(report.Blocks50);
        Assert.Empty(report.Characters);
        Assert.Empty(report.Months);
        Assert.Empty(report.Patches);
        Assert.Empty(report.MonthByCharacter);
        Assert.Empty(report.RecentRuns);
        Assert.Null(report.Trend);
        Assert.Null(report.Month);
        Assert.Null(report.Patch);
    }

    [Fact]
    public void First_and_last_run_bracket_the_archive()
    {
        var report = Report("WLW");

        Assert.Equal(new DateTime(2026, 1, 1), report.FirstRun!.Value.Date);
        Assert.Equal(new DateTime(2026, 1, 3), report.LastRun!.Value.Date);
    }

    // ── the recent window ────────────────────────────────────────────────────

    [Fact]
    public void The_recent_window_measures_the_last_fifty_runs()
    {
        // 100 losses, then 50 wins.
        var report = Report(new string('L', 100) + new string('W', 50));

        Assert.Equal(50, report.Recent.Runs);
        Assert.Equal(50, report.Recent.Wins);
        Assert.True(report.HasRecentWindow);
    }

    /// <summary>
    /// Below fifty runs the window is every run there is. It still reports, because the
    /// headline has to say something — but it admits it is not a window, so the screen does
    /// not draw a delta of a figure against itself.
    /// </summary>
    [Fact]
    public void A_short_archive_has_no_recent_window_to_speak_of()
    {
        var report = Report(new string('W', 30));

        Assert.Equal(30, report.Recent.Runs);
        Assert.Equal(report.Overall, report.Recent);
        Assert.False(report.HasRecentWindow);
    }

    [Fact]
    public void The_last_ten_runs_are_kept_whole_and_oldest_first()
    {
        var report = Report(new string('L', 20) + "WLWWLWLLLW");

        Assert.Equal(10, report.RecentRuns.Count);
        Assert.Equal(
            [true, false, true, true, false, true, false, false, false, true],
            report.RecentRuns.Select(run => run.Win));
        Assert.Equal(report.LastRun, report.RecentRuns[^1].LocalStart);
    }

    // ── streaks ──────────────────────────────────────────────────────────────

    [Fact]
    public void Current_streak_counts_back_from_the_newest_run()
    {
        var report = Report("WWLLWWW");

        Assert.Equal(3, report.CurrentStreak);
        Assert.True(report.CurrentStreakIsWin);
    }

    [Fact]
    public void A_losing_streak_is_reported_as_losses()
    {
        var report = Report("WWWLL");

        Assert.Equal(2, report.CurrentStreak);
        Assert.False(report.CurrentStreakIsWin);
    }

    [Fact]
    public void Longest_win_streak_ignores_the_streak_still_running_if_an_older_one_was_better()
    {
        var report = Report("WWWWLWW");

        Assert.Equal(4, report.LongestWinStreak);
        Assert.Equal(2, report.CurrentStreak);
    }

    [Fact]
    public void Longest_win_streak_is_zero_when_nothing_was_won()
    {
        Assert.Equal(0, Report("LLLL").LongestWinStreak);
    }

    // ── blocks ───────────────────────────────────────────────────────────────

    [Fact]
    public void Blocks_are_anchored_at_the_oldest_run_and_shown_newest_first()
    {
        var report = Report(new string('L', 125));

        Assert.Equal(3, report.Blocks50.Count);
        Assert.Equal("101-125", report.Blocks50[0].Label);
        Assert.Equal("51-100", report.Blocks50[1].Label);
        Assert.Equal("1-50", report.Blocks50[2].Label);
    }

    [Fact]
    public void The_partial_block_is_the_newest_one()
    {
        var newest = Report(new string('L', 125)).Blocks50[0];

        Assert.Equal(25, newest.Tally.Runs);
        Assert.Equal(new DateTime(2026, 1, 1).AddDays(100), newest.From.Date);
        Assert.Equal(new DateTime(2026, 1, 1).AddDays(124), newest.To.Date);
    }

    [Fact]
    public void Each_block_carries_the_all_time_rate_as_of_its_own_end()
    {
        // Fifty losses, then fifty wins: 0% after the first block, 50% after the second.
        var report = Report(new string('L', 50) + new string('W', 50));

        var newest = report.Blocks50[0];
        var oldest = report.Blocks50[1];

        Assert.Equal(1.0d, newest.Tally.WinRate, 6);
        Assert.Equal(0.5d, newest.CumulativeWinRate, 6);
        Assert.Equal(0d, oldest.Tally.WinRate, 6);
        Assert.Equal(0d, oldest.CumulativeWinRate, 6);
    }

    [Fact]
    public void The_newest_blocks_cumulative_rate_equals_the_overall_rate()
    {
        var report = Report("WLLWLWLLLWLLWLLLWLLWLLLW");

        Assert.Equal(report.Overall.WinRate, report.Blocks10[0].CumulativeWinRate, 6);
        Assert.Equal(report.Overall.WinRate, report.Blocks50[0].CumulativeWinRate, 6);
    }

    /// <summary>Both block sizes exist and follow the same rules at their own size.</summary>
    [Fact]
    public void Ten_run_blocks_cut_the_same_archive_more_finely()
    {
        var report = Report(new string('L', 25));

        Assert.Equal(3, report.Blocks10.Count);
        Assert.Equal(["21-25", "11-20", "1-10"], report.Blocks10.Select(block => block.Label));
        Assert.Equal(5, report.Blocks10[0].Tally.Runs);
        Assert.Single(report.Blocks50);
    }

    // ── characters ───────────────────────────────────────────────────────────

    [Fact]
    public void Characters_are_ranked_by_win_rate()
    {
        var runs = new List<RunRecord>();
        runs.AddRange(Sequence("WWLL", Start, "Ironclad"));      // 50%
        runs.AddRange(Sequence("WWWL", Start, "Silent"));        // 75%
        runs.AddRange(Sequence("LLLL", Start, "Defect"));        // 0%

        var characters = WinrateReport.Build(runs).Characters;

        Assert.Equal(["Silent", "Ironclad", "Defect"], characters.Select(row => row.Character));
        Assert.Equal(0.75d, characters[0].All.WinRate, 6);
    }

    [Fact]
    public void A_characters_recent_column_covers_its_own_last_ten_runs()
    {
        // Twelve runs: two old wins, then ten losses.
        var runs = Sequence("WW" + new string('L', 10), Start, "Regent");

        var row = Assert.Single(WinrateReport.Build(runs).Characters);

        Assert.Equal(12, row.All.Runs);
        Assert.Equal(2, row.All.Wins);
        Assert.Equal(10, row.Last10.Runs);
        Assert.Equal(0, row.Last10.Wins);
    }

    [Fact]
    public void A_character_with_fewer_than_ten_runs_reports_all_of_them_as_recent()
    {
        var row = Assert.Single(WinrateReport.Build(Sequence("WL", Start, "Regent")).Characters);

        Assert.Equal(2, row.Last10.Runs);
        Assert.Equal(1, row.Last10.Wins);
    }

    [Fact]
    public void A_characters_last_fifty_is_wider_than_its_last_ten()
    {
        // Sixty runs: fifty wins, then ten losses.
        var runs = Sequence(new string('W', 50) + new string('L', 10), Start, "Regent");

        var row = Assert.Single(WinrateReport.Build(runs).Characters);

        Assert.Equal(60, row.All.Runs);
        Assert.Equal(0, row.Last10.Wins);
        Assert.Equal(50, row.Last50.Runs);
        Assert.Equal(40, row.Last50.Wins);
    }

    // ── months ───────────────────────────────────────────────────────────────

    [Fact]
    public void Months_are_listed_newest_first()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 5), nodes: 20),
            Run(Unix(2026, 1, 6), nodes: 10),
            Run(Unix(2026, 2, 3), win: true, nodes: 40),
        };

        var months = WinrateReport.Build(runs).Months;

        Assert.Equal(["Feb 2026", "Jan 2026"], months.Select(row => row.Label));
        Assert.Equal(1, months[0].Tally.Wins);
        Assert.Equal(2, months[1].Tally.Runs);
        Assert.Equal(15d, months[1].AverageFloors, 6);
    }

    [Fact]
    public void A_months_cumulative_rate_covers_everything_up_to_its_end()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 5)),
            Run(Unix(2026, 1, 6)),
            Run(Unix(2026, 2, 3), win: true),
            Run(Unix(2026, 2, 4), win: true),
        };

        var months = WinrateReport.Build(runs).Months;

        // February alone is 100%, but half the archive by the end of it.
        Assert.Equal(1.0d, months[0].Tally.WinRate, 6);
        Assert.Equal(0.5d, months[0].CumulativeWinRate, 6);
        Assert.Equal(0d, months[1].CumulativeWinRate, 6);
    }

    // ── patches ──────────────────────────────────────────────────────────────

    [Fact]
    public void Hotfixes_share_a_row_with_their_patch()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 1), buildId: "v0.108.0"),
            Run(Unix(2026, 1, 2), win: true, buildId: "v0.108.1"),
            Run(Unix(2026, 1, 3), buildId: "v0.109.0"),
        };

        var patches = WinrateReport.Build(runs).Patches;

        Assert.Equal(["v0.109", "v0.108"], patches.Select(row => row.Label));
        Assert.Equal(2, patches[1].Tally.Runs);
        Assert.Equal(1, patches[1].Tally.Wins);
    }

    [Fact]
    public void Patches_sort_by_version_not_as_text()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 1), buildId: "v0.98.0"),
            Run(Unix(2026, 1, 2), buildId: "v0.100.0"),
            Run(Unix(2026, 1, 3), buildId: "v0.109.0"),
        };

        // As text, v0.98 would sort after v0.100.
        Assert.Equal(
            ["v0.109", "v0.100", "v0.98"],
            WinrateReport.Build(runs).Patches.Select(row => row.Label));
    }

    // ── month by character ───────────────────────────────────────────────────

    [Fact]
    public void The_matrix_runs_months_down_and_characters_across()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 5), win: true, character: "Ironclad"),
            Run(Unix(2026, 1, 6), character: "Silent"),
            Run(Unix(2026, 2, 5), character: "Ironclad"),
        };

        var report = WinrateReport.Build(runs);

        Assert.Equal(["Ironclad", "Silent"], report.MatrixCharacters);
        // Oldest month first, unlike the period tables: this one is read as a trend down
        // the page rather than as "what happened lately".
        Assert.Equal(["Jan 2026", "Feb 2026", "Total"], report.MonthByCharacter.Select(row => row.Label));
        Assert.Equal([new Tally(1, 1), new Tally(1, 0)], report.MonthByCharacter[0].Cells);
        // Silent never played in February, which stays null rather than becoming 0-0.
        Assert.Equal([new Tally(1, 0), null], report.MonthByCharacter[1].Cells);
        Assert.Equal([new Tally(2, 1), new Tally(1, 0)], report.MonthByCharacter[2].Cells);
    }

    // ── the trend ────────────────────────────────────────────────────────────

    [Fact]
    public void The_trend_blocks_fifty_runs_at_a_time_once_the_archive_is_big_enough()
    {
        var trend = Report(new string('L', 250)).Trend!;

        Assert.Equal(50, trend.BlockRuns);
        Assert.Equal(250, trend.Runs);
        Assert.Equal(["1", "51", "101", "151", "201"], trend.Blocks.Select(block => block.Label));
        Assert.Equal("201-250", trend.Blocks[^1].Range);
    }

    /// <summary>
    /// A player twenty runs in should get a trend rather than a single bar, so the block
    /// size follows the archive: five runs under sixty, ten under two hundred.
    /// </summary>
    [Theory]
    [InlineData(20, 5)]
    [InlineData(59, 5)]
    [InlineData(60, 10)]
    [InlineData(199, 10)]
    [InlineData(200, 50)]
    public void The_block_size_follows_the_size_of_the_archive(int runs, int expected)
    {
        Assert.Equal(expected, Report(new string('L', runs)).Trend!.BlockRuns);
    }

    /// <summary>
    /// Two runs at 100% would set the ceiling for every bar beside them, so a trailing
    /// sliver is dropped. A block that is merely short is kept and marked with a plus.
    /// </summary>
    [Fact]
    public void A_trailing_sliver_is_dropped_but_a_half_full_block_is_kept()
    {
        // 202 runs: four full blocks of fifty and two left over.
        var dropped = Report(new string('L', 202)).Trend!;

        Assert.Equal(200, dropped.Runs);
        Assert.Equal(4, dropped.Blocks.Count);
        Assert.Equal("151", dropped.Blocks[^1].Label);

        // 240 runs: the last block holds forty, which is worth plotting.
        var kept = Report(new string('L', 240)).Trend!;

        Assert.Equal(240, kept.Runs);
        Assert.Equal("201+", kept.Blocks[^1].Label);
        Assert.Equal("201-240", kept.Blocks[^1].Range);
    }

    [Fact]
    public void The_trend_reads_oldest_first_and_carries_the_running_all_time_rate()
    {
        var trend = Report(new string('L', 100) + new string('W', 100)).Trend!;

        Assert.Equal(0d, trend.Blocks[0].Tally.WinRate, 6);
        Assert.Equal(1.0d, trend.Blocks[^1].Tally.WinRate, 6);
        Assert.Equal(0d, trend.Blocks[0].CumulativeWinRate, 6);
        Assert.Equal(0.5d, trend.Blocks[^1].CumulativeWinRate, 6);
    }

    /// <summary>
    /// The ceiling clears the tallest bar rather than meeting it, so the best block reads as
    /// a value instead of as having run out of chart.
    /// </summary>
    [Fact]
    public void The_ceiling_is_the_next_ten_per_cent_above_the_tallest_bar()
    {
        // Blocks of fifty: 19 wins is 38%, 20 is exactly 40%.
        Assert.Equal(40, Report(Blocks50(19, 19, 19, 19)).Trend!.CeilingPercent);
        Assert.Equal(50, Report(Blocks50(20, 20, 20, 20)).Trend!.CeilingPercent);
        Assert.Equal(10, Report(new string('L', 200)).Trend!.CeilingPercent);
        Assert.Equal(100, Report(new string('W', 200)).Trend!.CeilingPercent);
    }

    /// <summary>Four fifty-run blocks, each with the given number of wins first.</summary>
    private static string Blocks50(params int[] wins) =>
        string.Concat(wins.Select(count => new string('W', count) + new string('L', 50 - count)));

    // ── this month, this patch ───────────────────────────────────────────────

    [Fact]
    public void The_month_comparison_is_the_newest_month_against_the_one_before()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 5)),
            Run(Unix(2026, 1, 6)),
            Run(Unix(2026, 2, 3), win: true),
        };

        var month = WinrateReport.Build(runs).Month!;

        Assert.Equal("Feb 2026", month.Current.Label);
        Assert.Equal("Jan 2026", month.Previous!.Label);
    }

    [Fact]
    public void The_first_month_has_nothing_behind_it_to_compare_with()
    {
        var report = Report("WL");

        Assert.Equal("Jan 2026", report.Month!.Current.Label);
        Assert.Null(report.Month.Previous);
        Assert.Null(report.Patch!.Previous);
    }

    [Fact]
    public void The_patch_comparison_follows_the_version_order_the_table_uses()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 1), buildId: "v0.98.0"),
            Run(Unix(2026, 1, 2), buildId: "v0.100.0"),
        };

        var patch = WinrateReport.Build(runs).Patch!;

        Assert.Equal("v0.100", patch.Current.Label);
        Assert.Equal("v0.98", patch.Previous!.Label);
    }

    /// <summary>
    /// A month carries a short form of its label for places with no room for the year. Only
    /// months do: a patch label is already as short as it gets, and a block's is a range
    /// that means nothing cut down.
    /// </summary>
    [Fact]
    public void Only_months_carry_a_short_label()
    {
        var report = Report("WL");

        Assert.Equal("Jan 2026", report.Months[0].Label);
        Assert.Equal("Jan", report.Months[0].ShortLabel);
        Assert.Equal("Jan", report.Months[0].Compact);

        Assert.Equal("", report.Patches[0].ShortLabel);
        Assert.Equal(report.Patches[0].Label, report.Patches[0].Compact);
        Assert.Equal("", report.Blocks50[0].ShortLabel);
        Assert.Equal(report.Blocks50[0].Label, report.Blocks50[0].Compact);
    }
}

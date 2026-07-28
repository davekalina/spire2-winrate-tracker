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
        Assert.Empty(report.Characters);
        Assert.Empty(report.CharacterByMonth);
    }

    [Fact]
    public void First_and_last_run_bracket_the_archive()
    {
        var report = Report("WLW");

        Assert.Equal(new DateTime(2026, 1, 1), report.FirstRun!.Value.Date);
        Assert.Equal(new DateTime(2026, 1, 3), report.LastRun!.Value.Date);
    }

    // ── trailing windows ─────────────────────────────────────────────────────

    [Fact]
    public void Trailing_windows_measure_the_most_recent_runs()
    {
        // 100 losses, then 10 wins.
        var report = Report(new string('L', 100) + new string('W', 10));

        var last10 = Assert.Single(report.TrailingWindows, window => window.Window == 10);
        Assert.Equal(10, last10.Tally.Wins);
        Assert.Equal(10, last10.Tally.Runs);

        var last25 = Assert.Single(report.TrailingWindows, window => window.Window == 25);
        Assert.Equal(10, last25.Tally.Wins);
        Assert.Equal(25, last25.Tally.Runs);
    }

    [Fact]
    public void A_window_covering_the_whole_archive_is_omitted_as_a_duplicate_of_overall()
    {
        // 30 runs: 10 and 25 fit inside, 50 and 100 would not.
        var windows = Report(new string('W', 30)).TrailingWindows.Select(window => window.Window).ToList();

        Assert.Equal([25, 10], windows);
    }

    [Fact]
    public void Windows_are_listed_largest_first()
    {
        var windows = Report(new string('W', 200)).TrailingWindows.Select(window => window.Window).ToList();

        Assert.Equal([100, 50, 25, 10], windows);
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
        var report = Report(new string('L', 25));

        Assert.Equal(3, report.Blocks10.Count);
        Assert.Equal("21-25", report.Blocks10[0].Label);
        Assert.Equal("11-20", report.Blocks10[1].Label);
        Assert.Equal("1-10", report.Blocks10[2].Label);
    }

    [Fact]
    public void The_partial_block_is_the_newest_one()
    {
        var newest = Report(new string('L', 25)).Blocks10[0];

        Assert.Equal(5, newest.Block.Runs);
        Assert.Equal(new DateTime(2026, 1, 21), newest.From.Date);
        Assert.Equal(new DateTime(2026, 1, 25), newest.To.Date);
    }

    [Fact]
    public void Each_block_carries_the_all_time_rate_as_of_its_own_end()
    {
        // Ten losses, then ten wins: 0% after the first block, 50% after the second.
        var report = Report(new string('L', 10) + new string('W', 10));

        var newest = report.Blocks10[0];
        var oldest = report.Blocks10[1];

        Assert.Equal(1.0d, newest.Block.WinRate, 6);
        Assert.Equal(0.5d, newest.CumulativeWinRate, 6);
        Assert.Equal(0d, oldest.Block.WinRate, 6);
        Assert.Equal(0d, oldest.CumulativeWinRate, 6);
    }

    [Fact]
    public void The_newest_blocks_cumulative_rate_equals_the_overall_rate()
    {
        var report = Report("WLLWLWLLLWLLWLLLWLLWLLLW");

        Assert.Equal(report.Overall.WinRate, report.Blocks10[0].CumulativeWinRate, 6);
        Assert.Equal(report.Overall.WinRate, report.Blocks50[0].CumulativeWinRate, 6);
    }

    [Fact]
    public void Fifty_run_blocks_use_the_same_rules_at_a_coarser_size()
    {
        var report = Report(new string('L', 120));

        Assert.Equal(3, report.Blocks50.Count);
        Assert.Equal("101-120", report.Blocks50[0].Label);
        Assert.Equal("51-100", report.Blocks50[1].Label);
        Assert.Equal("1-50", report.Blocks50[2].Label);
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
        Assert.Equal(10, row.Recent.Runs);
        Assert.Equal(0, row.Recent.Wins);
    }

    [Fact]
    public void A_character_with_fewer_than_ten_runs_reports_all_of_them_as_recent()
    {
        var row = Assert.Single(WinrateReport.Build(Sequence("WL", Start, "Regent")).Characters);

        Assert.Equal(2, row.Recent.Runs);
        Assert.Equal(1, row.Recent.Wins);
    }

    [Fact]
    public void Average_act_counts_a_win_as_act_four()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 1), win: true),
            Run(Unix(2026, 1, 2), actReached: 2),
        };

        Assert.Equal(3d, Assert.Single(WinrateReport.Build(runs).Characters).AverageAct, 6);
    }

    // ── months ───────────────────────────────────────────────────────────────

    [Fact]
    public void Months_are_listed_oldest_first_with_their_averages()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 2, 3), win: true, nodes: 40, elites: 4, runTimeSeconds: 3600f),
            Run(Unix(2026, 1, 5), nodes: 20, elites: 2, runTimeSeconds: 1800f),
            Run(Unix(2026, 1, 6), nodes: 10, elites: 0, runTimeSeconds: 600f),
        };

        var months = WinrateReport.Build(runs.OrderBy(run => run.StartTime).ToList()).Months;

        Assert.Equal(["2026-01", "2026-02"], months.Select(row => row.Month));
        Assert.Equal(2, months[0].Tally.Runs);
        Assert.Equal(0, months[0].Tally.Wins);
        Assert.Equal(15d, months[0].AverageNodes, 6);
        Assert.Equal(1d, months[0].AverageElites, 6);
        Assert.Equal(20d, months[0].AverageMinutes, 6);
        Assert.Equal(1, months[1].Tally.Wins);
        Assert.Equal(4d, months[1].AverageAct, 6);
    }

    // ── character by month ───────────────────────────────────────────────────

    [Fact]
    public void The_matrix_has_a_column_per_month_and_totals_underneath()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 5), win: true, character: "Ironclad"),
            Run(Unix(2026, 1, 6), character: "Silent"),
            Run(Unix(2026, 2, 5), character: "Ironclad"),
        };

        var report = WinrateReport.Build(runs.OrderBy(run => run.StartTime).ToList());

        Assert.Equal(["2026-01", "2026-02"], report.MatrixMonths);
        Assert.Equal(["Ironclad", "Silent", "Total", "Total %"], report.CharacterByMonth.Select(row => row.Label));
        Assert.Equal(["1/1", "0/1"], report.CharacterByMonth[0].Cells);
        // Silent never played in February.
        Assert.Equal(["0/1", "—"], report.CharacterByMonth[1].Cells);
        Assert.Equal(["1/2", "0/1"], report.CharacterByMonth[2].Cells);
        Assert.Equal(["50.0%", "0.0%"], report.CharacterByMonth[3].Cells);
    }

    // ── deaths ───────────────────────────────────────────────────────────────

    [Fact]
    public void Top_deaths_rank_the_encounters_that_ended_runs()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 1), killedBy: "Decimillipede"),
            Run(Unix(2026, 1, 2), killedBy: "Decimillipede"),
            Run(Unix(2026, 1, 3), killedBy: "Queen"),
            Run(Unix(2026, 1, 4), win: true),
        };

        var deaths = WinrateReport.Build(runs).TopDeaths;

        Assert.Equal([new CountRow("Decimillipede", 2), new CountRow("Queen", 1)], deaths);
    }

    [Fact]
    public void A_loss_with_no_recorded_killer_is_counted_as_unknown()
    {
        var deaths = WinrateReport.Build([Run(Unix(2026, 1, 1))]).TopDeaths;

        Assert.Equal(new CountRow("Unknown", 1), Assert.Single(deaths));
    }

    [Fact]
    public void Losses_by_act_are_ordered_by_act_and_exclude_wins()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 1), actReached: 3),
            Run(Unix(2026, 1, 2), actReached: 1),
            Run(Unix(2026, 1, 3), actReached: 1),
            Run(Unix(2026, 1, 4), win: true),
        };

        var byAct = WinrateReport.Build(runs).LossesByAct;

        Assert.Equal([new CountRow("Act 1", 2), new CountRow("Act 3", 1)], byAct);
    }
}

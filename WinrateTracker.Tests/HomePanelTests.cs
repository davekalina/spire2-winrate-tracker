using WinrateTracker.WinrateTrackerCode;
using Xunit;
using static WinrateTracker.Tests.TestRuns;

namespace WinrateTracker.Tests;

public class HomePanelTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    /// <summary>
    /// The chips are built from the archive under every filter <em>except</em> the character
    /// one, so unless a test says otherwise they come from the same runs as the report.
    /// </summary>
    private static HomePanel Panel(string results, string? selected = null)
    {
        var report = WinrateReport.Build(Sequence(results, Start));
        return HomePanel.Build(report, report.Characters, selected);
    }

    [Fact]
    public void An_empty_archive_builds_an_empty_panel_rather_than_dividing_by_zero()
    {
        var report = WinrateReport.Build([]);

        Assert.Equal(HomePanel.Empty, HomePanel.Build(report, report.Characters, null));
    }

    // ── the headline ─────────────────────────────────────────────────────────

    [Fact]
    public void The_headline_is_the_last_fifty_runs_against_the_whole_archive()
    {
        // 100 losses, then 18 wins and 32 losses.
        var panel = Panel(new string('L', 100) + new string('W', 18) + new string('L', 32));

        Assert.Equal("Last 50 runs", panel.RecentCaption);
        Assert.Equal("18-32", panel.RecentRecord);
        Assert.Equal("36.0%", panel.RecentRate);
        Assert.Equal("vs 12.0% all time", panel.RecentBaseline);
        Assert.Equal("▲ 24.0", panel.RecentDelta);
        Assert.Equal(Tone.Good, panel.RecentDeltaTone);
    }

    [Fact]
    public void A_headline_below_the_all_time_rate_reads_as_a_fall()
    {
        var panel = Panel(new string('W', 100) + new string('L', 50));

        Assert.Equal("0.0%", panel.RecentRate);
        Assert.Equal("▼ 66.7", panel.RecentDelta);
        Assert.Equal(Tone.Bad, panel.RecentDeltaTone);
    }

    /// <summary>
    /// Under fifty runs the headline is every run there is, so there is nothing to compare
    /// it with. It says so rather than drawing a delta of zero, which would read as "you
    /// have not improved" instead of "there is no window yet".
    /// </summary>
    [Fact]
    public void A_short_archive_gets_a_headline_with_no_delta_beside_it()
    {
        var panel = Panel("WLWLWLWLWL");

        Assert.Equal("Last 10 runs", panel.RecentCaption);
        Assert.Equal("50.0%", panel.RecentRate);
        Assert.Equal("", panel.RecentDelta);
        Assert.Equal("all time", panel.RecentBaseline);
    }

    [Fact]
    public void The_last_ten_runs_carry_everything_their_tip_reads_out()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 8, 24, 22), character: "Ironclad", ascension: 10,
                killedBy: "Queen Boss", actReached: 3, nodes: 44, runTimeSeconds: 3480f),
            Run(Unix(2026, 8, 29, 21), win: true, character: "Necrobinder", nodes: 50, runTimeSeconds: 4320f),
        };

        var report = WinrateReport.Build(runs);
        var panel = HomePanel.Build(report, report.Characters, null);

        var loss = panel.RecentRuns[0];
        Assert.False(loss.Win);
        Assert.Equal("Ironclad", loss.Character);
        Assert.Equal(10, loss.Ascension);
        Assert.Equal("2026-08-24 · 22:00", loss.When);
        Assert.Equal("58 min", loss.Length);
        Assert.Equal("Killed by Queen Boss", loss.Outcome);
        Assert.Equal("Act 3 · 44 floors", loss.Detail);

        var win = panel.RecentRuns[1];
        Assert.True(win.Win);
        Assert.Equal("Run won", win.Outcome);
        Assert.Equal("Act 3 cleared · 50 floors", win.Detail);
    }

    /// <summary>
    /// A run whose killer the file did not record still has to say something. "Killed by"
    /// with nothing after it reads as a bug in the mod rather than a gap in the save.
    /// </summary>
    [Fact]
    public void A_loss_with_no_recorded_killer_still_says_how_it_ended()
    {
        var report = WinrateReport.Build([Run(Unix(2026, 1, 1))]);

        Assert.Equal(
            "Killed by something unrecorded",
            HomePanel.Build(report, report.Characters, null).RecentRuns[0].Outcome);
    }

    // ── the trend ────────────────────────────────────────────────────────────

    [Fact]
    public void The_trend_titles_itself_with_the_runs_it_covers_and_labels_one_shared_axis()
    {
        // Four fifty-run blocks at 20% each, so the ceiling clears 20 and lands on 30.
        var panel = Panel(string.Concat(Enumerable.Repeat(new string('W', 10) + new string('L', 40), 4)));
        var trend = panel.Trend!;

        Assert.Equal("Trend (last 200 runs)", trend.Title);
        // Both series are percentages, so one axis serves them.
        Assert.Equal(["30%", "15%", "0%"], trend.AxisLabels);
        Assert.Equal(4, trend.Bars.Count);
        Assert.Equal(3, trend.TipLines.Count);
    }

    [Fact]
    public void Each_bar_is_a_fraction_of_the_ceiling_and_carries_its_own_tip()
    {
        // Four blocks of fifty — 200 runs is where the trend blocks fifty at a time. The
        // second is the best at 30%, so the ceiling is 40%.
        var weak = new string('W', 5) + new string('L', 45);
        var panel = Panel(weak + new string('W', 15) + new string('L', 35) + weak + weak);
        var bars = panel.Trend!.Bars;

        Assert.Equal(0.25d, bars[0].Height, 6);   // 10 of 40
        Assert.Equal(0.75d, bars[1].Height, 6);   // 30 of 40
        Assert.Equal(0.25d, bars[0].Cumulative, 6);
        Assert.Equal(0.50d, bars[1].Cumulative, 6); // 20% all-time, of 40

        Assert.Equal("runs 51-100", bars[1].TipHeading);
        Assert.Equal("15-35", bars[1].TipRecord);
        Assert.Equal("30%", bars[1].TipRate);
        Assert.Equal("all-time here 20.0%", bars[1].TipCumulative);
    }

    // ── character chips ──────────────────────────────────────────────────────

    [Fact]
    public void A_chip_shows_recent_form_and_colours_itself_by_the_last_ten()
    {
        var runs = new List<RunRecord>();
        runs.AddRange(Sequence("WWWWWLLLLL", Start, "Defect"));    // 5 of the last 10
        runs.AddRange(Sequence("WWWWLLLLLL", Start, "Silent"));    // 4
        runs.AddRange(Sequence("WWWLLLLLLL", Start, "Regent"));    // 3

        var report = WinrateReport.Build(runs);
        var chips = HomePanel.Build(report, report.Characters, null).Characters;

        Assert.Equal(["Defect", "Silent", "Regent"], chips.Select(chip => chip.Character));
        Assert.Equal([Tone.Good, Tone.Neutral, Tone.Bad], chips.Select(chip => chip.LastTenTone));
        Assert.Equal("5-5", chips[0].LastTenRecord);
        Assert.Equal("5-5 · 50%", chips[0].LastFifty);
        Assert.Equal([true, true, true, true, true, false, false, false, false, false], chips[0].RecentRuns);
    }

    /// <summary>
    /// Pressing a chip sets the screen's character filter, which narrows the report to that
    /// character — so the chips cannot be read from the report or the row would collapse to
    /// the one that was pressed. They come from the archive under every other filter, and
    /// the selection is the filter read back.
    /// </summary>
    [Fact]
    public void The_selected_chip_is_the_filter_read_back_and_the_others_stay_on_screen()
    {
        var everyCharacter = new List<RunRecord>();
        everyCharacter.AddRange(Sequence("WL", Start, "Ironclad"));
        everyCharacter.AddRange(Sequence("WW", Start, "Silent"));

        // The report is narrowed to Ironclad, the way the screen would have narrowed it.
        var narrowed = WinrateReport.Build(Sequence("WL", Start, "Ironclad"));
        var chips = HomePanel
            .Build(narrowed, WinrateReport.Build(everyCharacter).Characters, "Ironclad")
            .Characters;

        Assert.Equal(2, chips.Count);
        Assert.Equal([false, true], chips.Select(chip => chip.Selected));
        Assert.Equal("Ironclad", Assert.Single(chips, chip => chip.Selected).Character);
    }

    // ── the four boxes ───────────────────────────────────────────────────────

    [Fact]
    public void The_boxes_run_month_patch_streak_then_floors()
    {
        var runs = new List<RunRecord>
        {
            Run(Unix(2026, 1, 5), nodes: 20, buildId: "v0.108.0"),
            Run(Unix(2026, 1, 6), nodes: 30, buildId: "v0.108.0"),
            Run(Unix(2026, 2, 3), win: true, nodes: 40, buildId: "v0.109.0"),
            Run(Unix(2026, 2, 4), win: true, nodes: 50, buildId: "v0.109.0"),
        };

        var report = WinrateReport.Build(runs);
        var stats = HomePanel.Build(report, report.Characters, null).Stats;

        // Months use their short form here: four boxes share one row, and "Feb 2026"
        // three times over is most of a box spent on the year.
        Assert.Equal(
            ["This month · Feb", "This patch · v0.109", "Streak", "Avg floors · Feb"],
            stats.Select(stat => stat.Caption));

        Assert.Equal("100%", stats[0].Value);
        Assert.Equal("2-0", stats[0].Detail);
        Assert.Equal("▲ 100 vs Jan", stats[0].Delta);
        Assert.Equal(Tone.Good, stats[0].DeltaTone);

        Assert.Equal("2 wins", stats[2].Value);
        Assert.Equal(Tone.Good, stats[2].ValueTone);
        Assert.Equal("best 2 · last 02-04", stats[2].Delta);

        // Floors is a length, not a win rate, so it is drawn in the chart's colour rather
        // than the header gold a rate gets.
        Assert.Equal("45.0", stats[3].Value);
        Assert.Equal(Tone.Measured, stats[3].ValueTone);
        Assert.Equal("reached", stats[3].Detail);
        Assert.Equal("▲ 20.0 vs Jan", stats[3].Delta);
    }

    [Fact]
    public void A_box_with_nothing_behind_it_shows_no_delta()
    {
        var stats = Panel("WL").Stats;

        Assert.All(stats.Take(2), stat => Assert.Equal("", stat.Delta));
        Assert.Equal("", stats[^1].Delta);
    }

    [Fact]
    public void A_losing_streak_is_drawn_as_a_loss()
    {
        var stats = Panel("WWLLL").Stats;
        var streak = Assert.Single(stats, stat => stat.Caption == "Streak");

        Assert.Equal("3 losses", streak.Value);
        Assert.Equal(Tone.Bad, streak.ValueTone);
    }

    /// <summary>
    /// Under a time window the figures are not all-time figures, and the screen must not say
    /// they are. One phrase, from the filter, used by the headline and the column tips alike.
    /// </summary>
    [Fact]
    public void A_time_window_renames_what_the_comparison_is_against()
    {
        var runs = Sequence(new string('L', 100) + new string('W', 50), Start);
        var windowed = new RunFilter { WindowDays = 30 };
        var report = WinrateReport.Build(runs) with { Scope = windowed.Scope };

        var panel = HomePanel.Build(report, report.Characters, null);

        Assert.Equal("in the last 30 days", windowed.Scope);
        Assert.Equal("vs 33.3% in the last 30 days", panel.RecentBaseline);
    }

    [Fact]
    public void Without_a_window_the_comparison_is_all_time()
    {
        Assert.Equal("all time", RunFilter.Default.Scope);
        Assert.Equal(
            "vs 12.0% all time",
            Panel(new string('L', 100) + new string('W', 18) + new string('L', 32)).RecentBaseline);
    }
}

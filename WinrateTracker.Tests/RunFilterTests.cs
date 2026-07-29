using WinrateTracker.WinrateTrackerCode;
using Xunit;
using static WinrateTracker.Tests.TestRuns;

namespace WinrateTracker.Tests;

public class RunFilterTests
{
    private static readonly List<RunRecord> Archive =
    [
        Run(Unix(2026, 1, 3), win: true, character: "Ironclad", ascension: 10),
        Run(Unix(2026, 1, 1), character: "Silent", ascension: 10),
        Run(Unix(2026, 1, 2), character: "Ironclad", ascension: 8),
        // Abandoned deep into a run: a loss like any other.
        Run(Unix(2026, 1, 4), character: "Ironclad", ascension: 10, abandoned: true, nodes: 19),
        // Abandoned on floor 1: a reroll.
        Run(Unix(2026, 1, 6), character: "Ironclad", ascension: 10, abandoned: true, nodes: 1),
        Run(Unix(2026, 1, 5), win: true, character: "Ironclad", ascension: 10, playerCount: 2),
    ];

    private static RunFilter AllAscensions => new() { Ascension = null };

    [Fact]
    public void Applying_a_filter_sorts_oldest_first()
    {
        var runs = AllAscensions.Apply(Archive);

        Assert.Equal(
            [Unix(2026, 1, 1), Unix(2026, 1, 2), Unix(2026, 1, 3), Unix(2026, 1, 4)],
            runs.Select(run => run.StartTime));
    }

    [Fact]
    public void Co_op_runs_are_always_excluded()
    {
        Assert.DoesNotContain(AllAscensions.Apply(Archive), run => run.PlayerCount > 1);
    }

    [Fact]
    public void An_abandoned_run_counts_as_a_loss()
    {
        var abandoned = Assert.Single(AllAscensions.Apply(Archive), run => run.Abandoned);

        Assert.False(abandoned.Win);
    }

    [Fact]
    public void A_floor_one_abandon_is_dropped_by_default()
    {
        Assert.DoesNotContain(AllAscensions.Apply(Archive), run => run.IsEarlyAbandon);
    }

    [Fact]
    public void A_floor_one_abandon_can_be_kept()
    {
        var runs = new RunFilter { Ascension = null, IgnoreEarlyAbandons = false }.Apply(Archive);

        Assert.Contains(runs, run => run.IsEarlyAbandon);
    }

    [Fact]
    public void Only_the_first_floor_counts_as_an_early_abandon()
    {
        Assert.True(Run(Unix(2026, 1, 1), abandoned: true, nodes: 1).IsEarlyAbandon);
        Assert.False(Run(Unix(2026, 1, 1), abandoned: true, nodes: 2).IsEarlyAbandon);
        Assert.False(Run(Unix(2026, 1, 1), nodes: 1).IsEarlyAbandon);
    }

    [Fact]
    public void An_ascension_narrows_to_that_ascension_only()
    {
        var runs = new RunFilter { Ascension = 8 }.Apply(Archive);

        Assert.Equal(8, Assert.Single(runs).Ascension);
    }

    [Fact]
    public void A_character_narrows_to_that_character_only()
    {
        var runs = new RunFilter { Ascension = null, Character = "Silent" }.Apply(Archive);

        Assert.Equal("Silent", Assert.Single(runs).Character);
    }

    [Fact]
    public void The_default_filter_is_ascension_ten_solo_and_no_rerolls()
    {
        var runs = RunFilter.Default.Apply(Archive);

        Assert.Equal(3, runs.Count);
        Assert.All(runs, run =>
        {
            Assert.Equal(10, run.Ascension);
            Assert.Equal(1, run.PlayerCount);
            Assert.False(run.IsEarlyAbandon);
        });
    }

    // ── time window ──────────────────────────────────────────────────────────

    private static List<RunRecord> Spanning90Days =>
    [
        Run(Unix(2026, 1, 1)),
        Run(Unix(2026, 2, 15)),
        Run(Unix(2026, 3, 20)),
        Run(Unix(2026, 4, 1)),
    ];

    [Fact]
    public void A_null_window_keeps_the_whole_archive()
    {
        Assert.Equal(4, new RunFilter { Ascension = null }.Apply(Spanning90Days).Count);
    }

    [Fact]
    public void A_window_is_measured_back_from_the_newest_run()
    {
        // 30 days before 2026-04-01 reaches 2026-03-02, so only the last two runs qualify.
        var runs = new RunFilter { Ascension = null, WindowDays = 30 }.Apply(Spanning90Days);

        Assert.Equal([Unix(2026, 3, 20), Unix(2026, 4, 1)], runs.Select(run => run.StartTime));
    }

    [Fact]
    public void A_wider_window_reaches_further_back()
    {
        Assert.Equal(3, new RunFilter { Ascension = null, WindowDays = 60 }.Apply(Spanning90Days).Count);
        Assert.Equal(4, new RunFilter { Ascension = null, WindowDays = 120 }.Apply(Spanning90Days).Count);
    }

    [Fact]
    public void A_window_over_an_empty_archive_is_harmless()
    {
        Assert.Empty(new RunFilter { Ascension = 99, WindowDays = 30 }.Apply(Spanning90Days));
    }
}

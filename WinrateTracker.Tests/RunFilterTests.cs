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
    public void Co_op_runs_are_excluded_by_default()
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
    public void A_numbered_ascension_leaves_out_dread_runs_at_that_number()
    {
        var archive = new List<RunRecord>(Archive) { Run(Unix(2026, 1, 7), ascension: 10, dread: true) };

        Assert.DoesNotContain(new RunFilter { Ascension = 10 }.Apply(archive), run => run.Dread);
    }

    [Fact]
    public void Dread_narrows_to_dread_runs_only()
    {
        var archive = new List<RunRecord>(Archive)
        {
            Run(Unix(2026, 1, 7), ascension: 10, dread: true),
            Run(Unix(2026, 1, 8), ascension: 0, dread: true, win: true),
        };

        var runs = new RunFilter { Ascension = RunRecord.DreadLevel }.Apply(archive);

        Assert.Equal(2, runs.Count);
        Assert.All(runs, run => Assert.True(run.Dread));
    }

    [Fact]
    public void Every_ascension_together_includes_dread()
    {
        var archive = new List<RunRecord>(Archive) { Run(Unix(2026, 1, 7), dread: true) };

        Assert.Contains(AllAscensions.Apply(archive), run => run.Dread);
    }

    [Fact]
    public void The_highest_ascension_is_dread_once_a_dread_run_counts()
    {
        Assert.Equal(10, RunFilter.Default.HighestAscension(Archive));

        var archive = new List<RunRecord>(Archive) { Run(Unix(2026, 1, 7), dread: true) };
        Assert.Equal(RunRecord.DreadLevel, RunFilter.Default.HighestAscension(archive));
    }

    /// <summary>
    /// The screen opens on this, so a Dread run the rest of the filter drops must not pull
    /// it onto an empty Dread.
    /// </summary>
    [Fact]
    public void The_highest_ascension_comes_from_runs_the_rest_of_the_filter_keeps()
    {
        var archive = new List<RunRecord>(Archive)
        {
            Run(Unix(2026, 1, 7), dread: true, playerCount: 2),
            Run(Unix(2026, 1, 8), dread: true, abandoned: true, nodes: 1),
            Run(Unix(2026, 1, 9), dread: true, character: "Regent"),
        };

        Assert.Equal(10, new RunFilter { Character = "Ironclad" }.HighestAscension(archive));
        Assert.Equal(RunRecord.DreadLevel, new RunFilter { Mode = PlayerMode.Multiplayer }.HighestAscension(archive));
        Assert.Null(new RunFilter { Character = "Necrobinder" }.HighestAscension(archive));
    }

    [Fact]
    public void A_character_narrows_to_that_character_only()
    {
        var runs = new RunFilter { Ascension = null, Character = "Silent" }.Apply(Archive);

        Assert.Equal("Silent", Assert.Single(runs).Character);
    }

    /// <summary>
    /// The default names no ascension. Which one to open on is a fact about the archive —
    /// the highest actually played — and <c>FilterBar</c> selects it once the runs are in.
    /// Hard-coding one here would show a player who is not on it an empty screen.
    /// </summary>
    [Fact]
    public void The_default_filter_is_solo_and_no_rerolls_at_every_ascension()
    {
        var runs = RunFilter.Default.Apply(Archive);

        Assert.Null(RunFilter.Default.Ascension);
        Assert.Equal(PlayerMode.Singleplayer, RunFilter.Default.Mode);
        Assert.Contains(runs, run => run.Ascension != 10);
        Assert.All(runs, run =>
        {
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

    [Fact]
    public void Blitz_runs_do_not_count_and_everything_else_does()
    {
        Assert.False(RunFilter.Counts(Run(Unix(2026, 1, 1)) with { Blitz = true }));
        Assert.True(RunFilter.Counts(Run(Unix(2026, 1, 1))));
        // Rerolls and co-op are the filter's business, not this rule's.
        Assert.True(RunFilter.Counts(Run(Unix(2026, 1, 1), abandoned: true, nodes: 1)));
        Assert.True(RunFilter.Counts(Run(Unix(2026, 1, 1), playerCount: 2)));
    }
}

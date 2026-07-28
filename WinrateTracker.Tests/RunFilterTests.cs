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
        Run(Unix(2026, 1, 4), character: "Ironclad", ascension: 10, abandoned: true),
        Run(Unix(2026, 1, 5), win: true, character: "Ironclad", ascension: 10, playerCount: 2),
    ];

    [Fact]
    public void Applying_a_filter_sorts_oldest_first()
    {
        var runs = new RunFilter { Ascension = null }.Apply(Archive);

        Assert.Equal(
            [Unix(2026, 1, 1), Unix(2026, 1, 2), Unix(2026, 1, 3)],
            runs.Select(run => run.StartTime));
    }

    [Fact]
    public void Co_op_runs_are_always_excluded()
    {
        var runs = new RunFilter { Ascension = null, IncludeAbandoned = true }.Apply(Archive);

        Assert.DoesNotContain(runs, run => run.PlayerCount > 1);
        Assert.Equal(4, runs.Count);
    }

    [Fact]
    public void Abandoned_runs_are_excluded_by_default()
    {
        var runs = new RunFilter { Ascension = null }.Apply(Archive);

        Assert.DoesNotContain(runs, run => run.Abandoned);
    }

    [Fact]
    public void Abandoned_runs_can_be_included()
    {
        var runs = new RunFilter { Ascension = null, IncludeAbandoned = true }.Apply(Archive);

        Assert.Contains(runs, run => run.Abandoned);
    }

    [Fact]
    public void An_ascension_narrows_to_that_ascension_only()
    {
        var runs = new RunFilter { Ascension = 8 }.Apply(Archive);

        Assert.All(runs, run => Assert.Equal(8, run.Ascension));
        Assert.Single(runs);
    }

    [Fact]
    public void A_null_ascension_keeps_every_ascension()
    {
        var ascensions = new RunFilter { Ascension = null }.Apply(Archive).Select(run => run.Ascension).Distinct();

        Assert.Equal([10, 8], ascensions.Order().Reverse());
    }

    [Fact]
    public void A_character_narrows_to_that_character_only()
    {
        var runs = new RunFilter { Ascension = null, Character = "Silent" }.Apply(Archive);

        Assert.Equal("Silent", Assert.Single(runs).Character);
    }

    [Fact]
    public void Filters_combine()
    {
        var runs = new RunFilter { Ascension = 10, Character = "Ironclad" }.Apply(Archive);

        Assert.Equal(Unix(2026, 1, 3), Assert.Single(runs).StartTime);
    }

    [Fact]
    public void The_default_filter_is_ascension_ten_solo_and_finished()
    {
        var runs = RunFilter.Default.Apply(Archive);

        Assert.Equal(2, runs.Count);
        Assert.All(runs, run =>
        {
            Assert.Equal(10, run.Ascension);
            Assert.Equal(1, run.PlayerCount);
            Assert.False(run.Abandoned);
        });
    }
}

using System.Text.Json;
using WinrateTracker.WinrateTrackerCode;
using Xunit;
using static WinrateTracker.Tests.TestRuns;

namespace WinrateTracker.Tests;

public class MultiplayerTests
{
    // Deliberately beyond signed Int64 and double's exact integer range.
    private const ulong LocalId = ulong.MaxValue - 17;
    private static readonly IReadOnlyDictionary<string, ulong> LocalIds =
        new Dictionary<string, ulong> { ["steam"] = LocalId, ["none"] = 1 };

    private static string History(int playerCount, int localIndex, string platform = "steam") =>
        JsonSerializer.Serialize(new
        {
            start_time = 1700000000,
            platform_type = platform,
            win = true,
            players = Enumerable.Range(0, playerCount).Select(index => new
            {
                id = index == localIndex ? LocalId : (ulong)index + 1,
                // Matching characters must not cause a teammate's deck to be selected.
                character = "CHARACTER.SILENT",
                deck = new[]
                {
                    new { id = "CARD.STRIKE_SILENT", floor_added_to_deck = 1 },
                    new { id = index == localIndex ? "CARD.CLOAK_AND_DAGGER" : "CARD.FOOTWORK", floor_added_to_deck = 2 },
                },
                relics = new[]
                {
                    new { id = index == localIndex ? "RELIC.KUNAI" : "RELIC.SHURIKEN", floor_added_to_deck = 3 },
                },
            }),
        });

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    public void Multiplayer_uses_the_local_players_character_and_picks(int count, int localIndex)
    {
        var run = RunParser.Parse("1700000000.run", History(count, localIndex), LocalIds);

        Assert.NotNull(run);
        Assert.True(run.Win);
        Assert.Equal(count, run.PlayerCount);
        Assert.Equal("Silent", run.Character);
        Assert.Equal(["CLOAK_AND_DAGGER"], run.PickedCards);
        Assert.Equal(["KUNAI"], run.PickedRelics);
        Assert.Equal(new Tally(1, 1), WinrateReport.Build([run]).Overall);
    }

    [Fact]
    public void A_teammates_character_is_not_used_for_the_character_filter()
    {
        var json = History(2, 1).Replace("\"id\":1,\"character\":\"CHARACTER.SILENT\"",
            "\"id\":1,\"character\":\"CHARACTER.IRONCLAD\"");
        var run = RunParser.Parse("1700000000.run", json, LocalIds)!;

        Assert.True(new RunFilter { Mode = PlayerMode.Multiplayer, Character = "Silent" }.Matches(run));
        Assert.False(new RunFilter { Mode = PlayerMode.Multiplayer, Character = "Ironclad" }.Matches(run));
    }

    [Fact]
    public void Multiplayer_without_a_matching_local_id_never_falls_back_to_a_teammate()
    {
        Assert.Null(RunParser.Parse("1700000000.run", History(4, -1), LocalIds));
        Assert.Null(RunParser.Parse("1700000000.run", History(2, 1)));
    }

    [Fact]
    public void Player_identity_is_selected_for_the_saved_platform()
    {
        var run = RunParser.Parse("1700000000.run", History(2, 1, "none"), LocalIds);

        Assert.NotNull(run);
        Assert.Equal(["FOOTWORK"], run.PickedCards);
        Assert.Null(RunParser.Parse("1700000000.run", History(2, 1, "unknown"), LocalIds));
    }

    [Fact]
    public void Missing_platform_uses_the_games_offline_default()
    {
        var json = History(2, 1).Replace("\"platform_type\":\"steam\",", "");
        var run = RunParser.Parse("1700000000.run", json, LocalIds);

        Assert.NotNull(run);
        Assert.Equal(["FOOTWORK"], run.PickedCards);
    }

    [Fact]
    public void Old_solo_runs_do_not_require_player_identity()
    {
        var json = """{"players":[{"character":"CHARACTER.DEFECT"}],"win":true}""";

        Assert.Equal("Defect", RunParser.Parse("1700000000.run", json, LocalIds)!.Character);
    }

    [Theory]
    [InlineData((int)PlayerMode.Singleplayer, 3, 3, 3, 3)]
    [InlineData((int)PlayerMode.Multiplayer, 2, 1, 1, 1)]
    [InlineData((int)PlayerMode.All, 5, 4, 2, 2)]
    public void Mode_filters_the_same_report_and_streak_calculations(
        int mode, int runs, int wins, int current, int best)
    {
        var archive = new[]
        {
            Run(1, win: true),
            Run(2, win: true),
            Run(3, playerCount: 2),
            Run(4, win: true),
            Run(5, win: true, playerCount: 4),
        };
        var report = WinrateReport.Build(new RunFilter { Mode = (PlayerMode)mode }.Apply(archive));

        Assert.Equal(new Tally(runs, wins), report.Overall);
        Assert.Equal(current, report.CurrentStreak);
        Assert.True(report.CurrentStreakIsWin);
        Assert.Equal(best, report.LongestWinStreak);
    }

    [Theory]
    [InlineData((int)PlayerMode.Singleplayer, "1")]
    [InlineData((int)PlayerMode.Multiplayer, "3")]
    [InlineData((int)PlayerMode.All, "1,3")]
    public void Mode_composes_with_ascension_character_window_and_abandon_filters(int mode, string expected)
    {
        var archive = new[]
        {
            Run(1, character: "Silent"),
            Run(2, character: "Silent", playerCount: 2, ascension: 3),
            Run(3, character: "Silent", playerCount: 3),
            Run(4, playerCount: 4),
            Run(5, character: "Silent", playerCount: 2, abandoned: true, nodes: 1),
            Run(-900000, character: "Silent", playerCount: 2),
        };
        var filtered = new RunFilter { Mode = (PlayerMode)mode, Ascension = 10, Character = "Silent", WindowDays = 7 }.Apply(archive);

        Assert.Equal(expected, string.Join(",", filtered.Select(run => run.StartTime)));
    }
}

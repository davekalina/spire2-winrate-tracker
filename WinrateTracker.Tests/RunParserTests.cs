using WinrateTracker.WinrateTrackerCode;
using Xunit;

namespace WinrateTracker.Tests;

public class RunParserTests
{
    /// <summary>
    /// Shaped after a real v9 archive file: acts as an array per act, one room per map
    /// point, and the model ids the game writes.
    /// </summary>
    private const string LossJson = """
    {
      "schema_version": 9,
      "game_mode": "standard",
      "win": false,
      "start_time": 1785189461,
      "run_time": 4556.5,
      "ascension": 10,
      "build_id": "v0.109.1",
      "was_abandoned": false,
      "killed_by_encounter": "ENCOUNTER.INFESTED_PRISMS_ELITE",
      "players": [{ "character": "CHARACTER.NECROBINDER" }],
      "acts": ["ACT.OVERGROWTH", "ACT.HIVE"],
      "map_point_history": [
        [
          { "map_point_type": "ancient", "rooms": [{ "room_type": "event" }] },
          { "map_point_type": "monster", "rooms": [{ "room_type": "monster" }] },
          { "map_point_type": "elite", "rooms": [{ "room_type": "elite" }] },
          { "map_point_type": "rest_site", "rooms": [{ "room_type": "rest_site" }] },
          { "map_point_type": "shop", "rooms": [{ "room_type": "shop" }] },
          { "map_point_type": "boss", "rooms": [{ "room_type": "boss" }] }
        ],
        [
          { "map_point_type": "unknown", "rooms": [{ "room_type": "event" }, { "room_type": "monster" }] },
          { "map_point_type": "elite", "rooms": [{ "room_type": "elite" }] }
        ]
      ]
    }
    """;

    private static RunRecord Parse(string json, string fileName = "1785189461.run") =>
        RunParser.Parse(fileName, json) ?? throw new InvalidOperationException("expected the run to parse");

    [Fact]
    public void Reads_the_headline_fields()
    {
        var run = Parse(LossJson);

        Assert.Equal("1785189461.run", run.FileName);
        Assert.Equal(1785189461, run.StartTime);
        Assert.Equal(10, run.Ascension);
        Assert.False(run.Win);
        Assert.False(run.Abandoned);
        Assert.Equal("Necrobinder", run.Character);
        Assert.Equal(1, run.PlayerCount);
        Assert.Equal(4556.5f, run.RunTimeSeconds);
    }

    [Fact]
    public void Counts_map_points_not_rooms_for_run_length()
    {
        // Eight map points, one of which resolved into two rooms.
        Assert.Equal(8, Parse(LossJson).Nodes);
    }

    [Fact]
    public void Counts_every_room_a_map_point_resolved_into()
    {
        var run = Parse(LossJson);

        Assert.Equal(2, run.Elites);
        Assert.Equal(1, run.Bosses);
        // monster + elite + boss + the monster the Unknown turned into + its second elite.
        Assert.Equal(5, run.Combats);
        Assert.Equal(1, run.Shops);
        Assert.Equal(1, run.Rests);
        Assert.Equal(2, run.Events);
    }

    [Fact]
    public void Act_reached_counts_acts_entered_on_a_loss()
    {
        Assert.Equal(2, Parse(LossJson).ActReached);
    }

    [Fact]
    public void Act_reached_is_four_on_a_win_and_the_death_is_cleared()
    {
        var json = LossJson
            .Replace("\"win\": false", "\"win\": true")
            .Replace("\"killed_by_encounter\": \"ENCOUNTER.INFESTED_PRISMS_ELITE\"", "\"killed_by_encounter\": \"NONE.NONE\"");

        var run = Parse(json);

        Assert.Equal(4, run.ActReached);
        Assert.Equal("", run.KilledBy);
    }

    [Fact]
    public void Killer_keeps_the_elite_suffix_that_says_which_fight_it_was()
    {
        Assert.Equal("Infested Prisms Elite", Parse(LossJson).KilledBy);
    }

    [Fact]
    public void A_win_never_reports_a_killer_even_if_the_file_names_one()
    {
        var json = LossJson.Replace("\"win\": false", "\"win\": true");
        Assert.Equal("", Parse(json).KilledBy);
    }

    [Fact]
    public void A_loss_with_no_recorded_encounter_reports_no_killer()
    {
        var json = LossJson.Replace("\"killed_by_encounter\": \"ENCOUNTER.INFESTED_PRISMS_ELITE\"", "\"killed_by_encounter\": \"NONE.NONE\"");
        Assert.Equal("", Parse(json).KilledBy);
    }

    [Fact]
    public void Accepts_a_flat_map_point_history()
    {
        var json = """
        {
          "win": false,
          "start_time": 1700000000,
          "ascension": 8,
          "players": [{ "character": "CHARACTER.SILENT" }],
          "map_point_history": [
            { "map_point_type": "monster", "rooms": [{ "room_type": "monster" }] },
            { "map_point_type": "elite", "rooms": [{ "room_type": "elite" }] }
          ]
        }
        """;

        var run = Parse(json, "1700000000.run");

        Assert.Equal(2, run.Nodes);
        Assert.Equal(1, run.Elites);
        Assert.Equal(1, run.ActReached);
    }

    [Fact]
    public void Empty_acts_do_not_count_as_acts_entered()
    {
        var json = """
        {
          "win": false,
          "start_time": 1700000000,
          "players": [{ "character": "CHARACTER.DEFECT" }],
          "map_point_history": [
            [{ "map_point_type": "monster", "rooms": [{ "room_type": "monster" }] }],
            [],
            []
          ]
        }
        """;

        Assert.Equal(1, Parse(json, "1700000000.run").ActReached);
    }

    [Fact]
    public void Falls_back_to_the_file_name_when_the_start_time_is_missing()
    {
        var json = """{ "win": true, "players": [{ "character": "CHARACTER.REGENT" }] }""";
        Assert.Equal(1780074232, Parse(json, "1780074232.run").StartTime);
    }

    [Fact]
    public void Missing_fields_take_defaults_rather_than_throwing()
    {
        var run = Parse("""{ "start_time": 1700000000 }""");

        Assert.Equal(0, run.Ascension);
        Assert.False(run.Win);
        Assert.Equal("", run.Character);
        Assert.Equal(0, run.PlayerCount);
        Assert.Equal(0, run.Nodes);
        Assert.Equal(1, run.ActReached);
        Assert.Equal("", run.KilledBy);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("")]
    public void Unreadable_files_are_skipped_rather_than_throwing(string json)
    {
        Assert.Null(RunParser.Parse("bad.run", json));
    }

    [Fact]
    public void A_file_with_no_start_time_and_no_numeric_name_is_skipped()
    {
        Assert.Null(RunParser.Parse("keep-this.run", """{ "win": true }"""));
    }

    [Fact]
    public void A_co_op_run_without_player_identity_is_skipped()
    {
        var json = """
        {
          "start_time": 1700000000,
          "players": [{ "character": "CHARACTER.IRONCLAD" }, { "character": "CHARACTER.SILENT" }]
        }
        """;

        Assert.Null(RunParser.Parse("1700000000.run", json));
    }

    [Theory]
    [InlineData("CHARACTER.NECROBINDER", "Necrobinder")]
    [InlineData("ENCOUNTER.INFESTED_PRISMS_ELITE", "Infested Prisms Elite")]
    [InlineData("ACT.OVERGROWTH", "Overgrowth")]
    [InlineData("NO_PREFIX_HERE", "No Prefix Here")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Model_ids_read_as_words(string? raw, string expected)
    {
        Assert.Equal(expected, RunParser.CleanId(raw));
    }
}

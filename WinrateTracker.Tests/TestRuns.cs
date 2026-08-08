using WinrateTracker.WinrateTrackerCode;

namespace WinrateTracker.Tests;

/// <summary>Builds run records for tests without repeating every required field.</summary>
internal static class TestRuns
{
    /// <summary>
    /// Start times are built from a local <see cref="DateTime" /> and read back through
    /// <see cref="RunRecord.LocalStart" />, so month and date assertions hold in any time
    /// zone.
    /// </summary>
    public static long Unix(int year, int month, int day, int hour = 12) =>
        new DateTimeOffset(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Local)).ToUnixTimeSeconds();

    public static RunRecord Run(
        long startTime,
        bool win = false,
        string character = "Ironclad",
        int ascension = 10,
        bool abandoned = false,
        int playerCount = 1,
        int actReached = 1,
        int nodes = 17,
        int elites = 2,
        float runTimeSeconds = 1800f,
        string killedBy = "",
        string buildId = "v0.109.1",
        IReadOnlyList<string>? cards = null,
        IReadOnlyList<string>? relics = null) =>
        new()
        {
            FileName = $"{startTime}.run",
            StartTime = startTime,
            Ascension = ascension,
            Win = win,
            Abandoned = abandoned,
            Character = character,
            PlayerCount = playerCount,
            RunTimeSeconds = runTimeSeconds,
            Nodes = nodes,
            ActReached = win ? 4 : actReached,
            Elites = elites,
            KilledBy = win ? "" : killedBy,
            PickedCards = cards ?? [],
            PickedRelics = relics ?? [],
            Patch = RunParser.PatchOf(buildId).Patch,
            PatchOrder = RunParser.PatchOf(buildId).Order,
        };

    /// <summary>
    /// <paramref name="results" /> reads oldest to newest: <c>W</c> a win, <c>L</c> a
    /// loss. One run per day from <paramref name="start" />.
    /// </summary>
    public static List<RunRecord> Sequence(string results, DateTime start, string character = "Ironclad")
    {
        var runs = new List<RunRecord>(results.Length);
        for (var i = 0; i < results.Length; i++)
        {
            var day = start.AddDays(i);
            runs.Add(Run(Unix(day.Year, day.Month, day.Day), win: results[i] == 'W', character: character));
        }
        return runs;
    }
}

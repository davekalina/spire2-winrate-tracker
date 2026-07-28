namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// One finished run, reduced to the fields the win rate tables need.
///
/// A <c>.run</c> file is around 56 KB, most of it the deck, the relics, and the
/// per-floor player stats. None of that is needed to answer "how often do I win", so a
/// run is read once, reduced to this, and cached — see <c>RunArchive</c>. Keeping the
/// record small is what makes re-opening the screen instant across a 500-run archive.
/// </summary>
internal sealed record RunRecord
{
    /// <summary>The archive file this came from, e.g. <c>1785189461.run</c>. Cache key.</summary>
    public required string FileName { get; init; }

    /// <summary>Unix seconds. The game names the file after it, so it is also the sort key.</summary>
    public required long StartTime { get; init; }

    public required int Ascension { get; init; }
    public required bool Win { get; init; }
    public required bool Abandoned { get; init; }

    /// <summary>Display name, e.g. <c>Necrobinder</c>. See <see cref="RunParser.CleanId" />.</summary>
    public required string Character { get; init; }

    /// <summary>1 for a solo run. Co-op runs are excluded from the tables; see <see cref="RunFilter" />.</summary>
    public required int PlayerCount { get; init; }

    public required float RunTimeSeconds { get; init; }

    /// <summary>Map points entered across every act. The run's length.</summary>
    public required int Nodes { get; init; }

    /// <summary>
    /// 1-3 for a loss, 4 for a win. A win is its own bucket because "reached Act 3" and
    /// "beat Act 3" are the two outcomes worth telling apart in a losses-by-act table.
    /// </summary>
    public required int ActReached { get; init; }

    public int Elites { get; init; }
    public int Bosses { get; init; }

    /// <summary>Monster, elite, and boss rooms together.</summary>
    public int Combats { get; init; }

    public int Shops { get; init; }
    public int Rests { get; init; }
    public int Events { get; init; }

    /// <summary>Display name of the encounter that ended the run, or empty on a win.</summary>
    public string KilledBy { get; init; } = "";

    /// <summary>Local wall-clock start. Month and date grouping both come from this.</summary>
    public DateTime LocalStart => DateTimeOffset.FromUnixTimeSeconds(StartTime).ToLocalTime().DateTime;

    /// <summary><c>yyyy-MM</c>, the key the per-month table groups on.</summary>
    public string Month => LocalStart.ToString("yyyy-MM");

    public float RunTimeMinutes => RunTimeSeconds / 60f;
}

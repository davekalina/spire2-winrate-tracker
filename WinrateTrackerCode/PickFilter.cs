namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// What the Cards and Relics tabs show, on top of the run filter the whole screen shares.
///
/// These narrow the rows rather than the runs, which is why they are separate from
/// <see cref="RunFilter" />: excluding a card from the list must not change the win rate of
/// the runs that took it, and a run filter would do exactly that.
///
/// The rarities are held as the names the tables display rather than as an enum. The
/// vocabularies differ between cards and relics, both come from the game rather than from
/// here, and a mod or a patch can add to either — a name matches whatever the game says
/// today.
/// </summary>
internal sealed record PickFilter
{
    public static readonly PickFilter Default = new();

    /// <summary>Rarity value meaning "do not filter on rarity".</summary>
    public const string AnyRarity = "";

    /// <summary>
    /// Hide anything picked fewer times than this. One shows everything, which is the
    /// default because a truthful long list is a better starting point than a short one
    /// that has quietly decided what counts.
    /// </summary>
    public int MinimumPicks { get; init; } = 1;

    public string CardRarity { get; init; } = AnyRarity;

    public string RelicRarity { get; init; } = AnyRarity;

    public IReadOnlyList<PickRow> ApplyToCards(IReadOnlyList<PickRow> picks) =>
        Apply(picks, CardRarity);

    public IReadOnlyList<PickRow> ApplyToRelics(IReadOnlyList<PickRow> picks) =>
        Apply(picks, RelicRarity);

    private IReadOnlyList<PickRow> Apply(IReadOnlyList<PickRow> picks, string rarity) =>
        picks
            .Where(pick => pick.Tally.Runs >= MinimumPicks)
            .Where(pick => rarity == AnyRarity || string.Equals(pick.Rarity, rarity, StringComparison.Ordinal))
            .ToList();

    /// <summary>
    /// The rarities actually present, weakest first, for the filter control to offer. Built from
    /// the data rather than from the enum so the list never offers a rarity that would
    /// select nothing — the same rule the ascension and character filters follow.
    /// </summary>
    public static IReadOnlyList<string> RaritiesIn(IReadOnlyList<PickRow> picks) =>
        picks
            .Select(pick => pick.Rarity)
            .Where(rarity => rarity != GameData.UnknownRarity)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(GameData.RarityRank)
            .ThenBy(rarity => rarity, StringComparer.Ordinal)
            .ToList();
}

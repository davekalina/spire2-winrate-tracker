using WinrateTracker.WinrateTrackerCode;
using Xunit;

namespace WinrateTracker.Tests;

public class PickFilterTests
{
    private static PickRow Pick(string id, string rarity, int runs, int wins) =>
        new(id, rarity, new Tally(runs, wins));

    private static readonly IReadOnlyList<PickRow> Picks =
    [
        Pick("SHIV", "Common", 9, 6),
        Pick("KUNAI", "Uncommon", 4, 3),
        Pick("ECHO_FORM", "Rare", 1, 1),
        Pick("MYSTERY", GameData.UnknownRarity, 3, 0),
    ];

    [Fact]
    public void The_default_filter_hides_nothing()
    {
        Assert.Equal(Picks.Count, PickFilter.Default.ApplyToCards(Picks).Count);
    }

    [Fact]
    public void A_minimum_drops_everything_picked_fewer_times()
    {
        var filtered = new PickFilter { MinimumPicks = 4 }.ApplyToCards(Picks);

        Assert.Equal(["SHIV", "KUNAI"], filtered.Select(pick => pick.Id));
    }

    [Fact]
    public void A_rarity_selects_only_that_rarity()
    {
        var filtered = new PickFilter { CardRarity = "Uncommon" }.ApplyToCards(Picks);

        Assert.Equal("KUNAI", Assert.Single(filtered).Id);
    }

    [Fact]
    public void The_card_rarity_and_the_relic_rarity_are_independent()
    {
        var filter = new PickFilter { CardRarity = "Common", RelicRarity = "Rare" };

        Assert.Equal("SHIV", Assert.Single(filter.ApplyToCards(Picks)).Id);
        Assert.Equal("ECHO_FORM", Assert.Single(filter.ApplyToRelics(Picks)).Id);
    }

    [Fact]
    public void The_offered_rarities_are_the_ones_present_weakest_first()
    {
        Assert.Equal(["Common", "Uncommon", "Rare"], PickFilter.RaritiesIn(Picks));
    }

    /// <summary>
    /// Rarity is unknown when the game's models are not loaded. Offering it as a choice
    /// would put an em dash in the filter, so it is left out — but the rows themselves stay
    /// visible, because hiding a card for want of a rarity would be worse.
    /// </summary>
    [Fact]
    public void An_unknown_rarity_is_never_offered_but_is_still_listed()
    {
        Assert.DoesNotContain(GameData.UnknownRarity, PickFilter.RaritiesIn(Picks));
        Assert.Contains(PickFilter.Default.ApplyToCards(Picks), pick => pick.Id == "MYSTERY");
    }
}

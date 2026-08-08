using Godot;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The second filter row, which belongs to the Cards and Relics tabs and is hidden
/// everywhere else.
///
/// It is a row of its own rather than more widgets on the shared row because these narrow
/// the lists rather than the runs. Keeping them apart also keeps the tab's own question —
/// which picks am I looking at — visually separate from the archive-wide one above it.
///
/// One rarity control serves both tabs, reading and writing whichever of the two the open
/// tab owns. Cards and relics do not share a rarity vocabulary and are never on screen
/// together, so a second control would only ever be the wrong one.
///
/// The rarity list is built from the picks actually on screen, so it never offers a rarity
/// that would select nothing. That is the same rule the ascension and character filters
/// follow.
/// </summary>
internal sealed class PickFilterBar
{
    /// <summary>
    /// Minimums offered. They climb rather than step evenly because the interesting
    /// question moves fast: the difference between one pick and three is most of the noise,
    /// and past ten it is about how much evidence you insist on.
    /// </summary>
    private static readonly int[] Minimums = [1, 2, 3, 5, 10, 20, 50];

    private const string AnyRarityText = "All";

    private readonly PaginatorRow _row = new();
    private readonly PaginatorRow.Cycler _minimum;
    private readonly PaginatorRow.Cycler _rarity;

    public PickFilterBar()
    {
        _minimum = _row.Add("Minimum picks");
        _rarity = _row.Add("Rarity");

        _row.Changed += Publish;
        _row.Changed += () => Changed?.Invoke();
    }

    public Control Root => _row.Root;

    public List<Control> Controls => _row.Controls;

    /// <summary>Raised after the player pages any control. The screen rebuilds its tables.</summary>
    public event Action? Changed;

    /// <summary>
    /// Refill the rarity lists from a report. Called whenever the archive or the run filter
    /// changes, because either can change which rarities are on screen.
    /// </summary>
    public void Rebuild(WinrateReport report)
    {
        var filter = WinrateSession.Picks;
        var relics = ShowingRelics;

        _minimum.SetOptions(
            Minimums.Select(minimum => new PaginatorRow.Option(MinimumText(minimum), minimum)),
            filter.MinimumPicks);

        _rarity.SetOptions(
            RarityOptions(relics ? report.Relics : report.Cards),
            relics ? filter.RelicRarity : filter.CardRarity);

        // The selection can move when a list shrinks — a rarity that is no longer on screen
        // really has fallen back to All — so what the control now reads has to be written
        // back rather than assumed.
        Publish();
    }

    private static bool ShowingRelics => WinrateSession.Tab == ReportTab.Relics;

    private static string MinimumText(int minimum) =>
        minimum == 1 ? "Any" : $"{minimum}+";

    private static IEnumerable<PaginatorRow.Option> RarityOptions(IReadOnlyList<PickRow> picks) =>
        PickFilter.RaritiesIn(picks)
            .Select(rarity => new PaginatorRow.Option(rarity, rarity))
            .Prepend(new PaginatorRow.Option(AnyRarityText, PickFilter.AnyRarity));

    /// <summary>
    /// Write the row back to the session. The rarity control only speaks for the tab it is
    /// showing, so the other tab's rarity is carried through untouched — switching tabs
    /// finds each list still narrowed the way it was left.
    /// </summary>
    private void Publish()
    {
        var rarity = _rarity.Selected as string ?? PickFilter.AnyRarity;
        var current = WinrateSession.Picks;

        WinrateSession.Picks = new PickFilter
        {
            MinimumPicks = _minimum.Selected as int? ?? 1,
            CardRarity = ShowingRelics ? current.CardRarity : rarity,
            RelicRarity = ShowingRelics ? rarity : current.RelicRarity,
        };
    }
}

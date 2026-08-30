using Godot;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The two filters that belong to the Cards and Relics tabs, hidden everywhere else.
///
/// They sit on the end of the shared filter row behind a divider rather than on a second
/// row of their own. As arrow pairs they needed one — five filters was ten focus stops and
/// more width than the row had — but as combo boxes all five fit on one line, and the
/// divider is enough to say that these two narrow the list while the three before them
/// narrow the runs.
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
    private const int Separation = 14;

    private readonly HBoxContainer _row;
    private readonly ComboBox _minimum;
    private readonly ComboBox _rarity;

    public PickFilterBar(Control host)
    {
        _row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        _row.AddThemeConstantOverride("separation", Separation);

        // A hairline between the filters that narrow runs and the ones that narrow rows.
        _row.AddChild(new ColorRect
        {
            Color = NativeStyle.CellColor with { A = 0.16f },
            CustomMinimumSize = new Vector2(2, DividerHeight),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        _minimum = Add(host, "Min picks");
        _rarity = Add(host, "Rarity");

        foreach (var combo in Combos)
        {
            combo.Changed += Publish;
            combo.Changed += () => Changed?.Invoke();
        }
    }

    private const float DividerHeight = 34f;

    private ComboBox Add(Control host, string caption)
    {
        var combo = new ComboBox(host, caption);
        _row.AddChild(combo.Root);
        return combo;
    }

    private IEnumerable<ComboBox> Combos => [_minimum, _rarity];

    public Control Root => _row;

    public List<Control> Controls => Combos.Select(combo => combo.Root).ToList();

    public void Close()
    {
        foreach (var combo in Combos)
            combo.Close();
    }

    /// <summary>Raised after the player commits a filter. The screen rebuilds its tables.</summary>
    public event Action? Changed;

    /// <summary>
    /// Refill the rarity lists from a report. Called whenever the archive or the run filter
    /// changes, because either can change which rarities are on screen.
    /// </summary>
    public void Rebuild(WinrateReport report)
    {
        var filter = WinrateSession.Picks;
        var relics = ShowingRelics;
        var table = relics ? GameData.Relics : GameData.Cards;

        _minimum.SetOptions(
            Minimums.Select(minimum => new ComboBox.Option(MinimumText(minimum), minimum)),
            filter.MinimumPicks);

        _rarity.SetOptions(
            RarityOptions(relics ? report.Relics : report.Cards, table),
            relics ? filter.RelicRarity : filter.CardRarity);

        // The selection can move when a list shrinks — a rarity that is no longer on screen
        // really has fallen back to All — so what the control now reads has to be written
        // back rather than assumed.
        Publish();
    }

    private static bool ShowingRelics => WinrateSession.Tab == ReportTab.Relics;

    private static string MinimumText(int minimum) =>
        minimum == 1 ? "Any" : $"{minimum}+";

    private static IEnumerable<ComboBox.Option> RarityOptions(IReadOnlyList<PickRow> picks, string table) =>
        PickFilter.RaritiesIn(picks)
            .Select(rarity => new ComboBox.Option(rarity, rarity, ArtKey.Rarity(table, rarity)))
            .Prepend(new ComboBox.Option(AnyRarityText, PickFilter.AnyRarity));

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

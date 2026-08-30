using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The row that decides which runs the tables cover: ascension, character, and how far back
/// to look. It applies to every tab.
///
/// Options come from the archive itself, so the ascension list only offers ascensions that
/// have actually been played and the character list only characters that have been played.
/// </summary>
internal sealed class FilterBar
{
    /// <summary>
    /// Windows offered, in order. Null is the whole archive.
    ///
    /// These carry their own unit because the control has no caption: <c>30 days</c> reads
    /// on its own where a bare <c>30</c> would not, and <c>All-Time</c> says what "no window"
    /// means better than the word "All" beside a label saying "Window".
    ///
    /// A window the player types is still not here: it needs a number entry, which this row
    /// has no room for and the game has no native control for.
    /// </summary>
    private static readonly (string Text, int? Days)[] Windows =
    [
        ("All-Time", null),
        ("7 days", 7),
        ("14 days", 14),
        ("30 days", 30),
        ("45 days", 45),
        ("60 days", 60),
        ("90 days", 90),
        ("120 days", 120),
    ];

    /// <summary>
    /// No filter on this row carries a caption. Each one's values say what they are —
    /// "A10", "All Characters", "30 days" — and a word in front of them was width spent
    /// repeating what the value already said.
    ///
    /// Ascension held out longest, because a bare "10" is a number with no subject. The
    /// answer was to write the value properly rather than to label it: the game itself
    /// says A10.
    /// </summary>
    private const string NoCaption = "";

    private const string AllAscensions = "All Ascensions";
    private const string AllCharacters = "All Characters";

    private readonly HBoxContainer _row;
    private readonly ComboBox _ascension;
    private readonly ComboBox _character;
    private readonly ComboBox _window;

    /// <summary>
    /// Whether the player has chosen an ascension for themselves.
    ///
    /// Until they have, the screen picks the highest one in the archive — most people who
    /// open this mod sit on one ascension and want that one. After they have, their choice
    /// stands, including through a background reload that would otherwise quietly put them
    /// back on the highest.
    /// </summary>
    private bool _ascensionChosen;

    public FilterBar(Control host)
    {
        _row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        _row.AddThemeConstantOverride("separation", Separation);

        _ascension = Add(host, NoCaption);
        _character = Add(host, NoCaption);
        _window = Add(host, NoCaption);

        _ascension.Changed += () => _ascensionChosen = true;
        foreach (var combo in Combos)
        {
            // Publish first: the screen's handler reads the filter this writes.
            combo.Changed += Publish;
            combo.Changed += () => Changed?.Invoke();
        }

        Rebuild();
    }

    private const int Separation = 14;

    private ComboBox Add(Control host, string caption)
    {
        var combo = new ComboBox(host, caption);
        _row.AddChild(combo.Root);
        return combo;
    }

    private IEnumerable<ComboBox> Combos => [_ascension, _character, _window];

    public Control Root => _row;

    /// <summary>
    /// The row's focus stops, left to right — one per filter, which is the reason these are
    /// combo boxes rather than the arrow pairs they replaced.
    /// </summary>
    public List<Control> Controls =>
        Combos.Where(combo => combo.Root.Visible).Select(combo => combo.Root).ToList();

    public void FocusFirst() => Controls.FirstOrDefault()?.TryGrabFocus();

    /// <summary>Close whatever list is open. The screen calls this when the tab changes.</summary>
    public void Close()
    {
        foreach (var combo in Combos)
            combo.Close();
    }

    /// <summary>
    /// Hide the character filter on the tab that is already a comparison between characters.
    /// Narrowing to one there would leave a table of a single row.
    /// </summary>
    public void ShowCharacter(bool visible)
    {
        // Closed before it is hidden: a list left open on a control that has just gone
        // invisible would still be on screen, still holding cancel, with nothing to
        // dismiss it back to.
        if (!visible)
            _character.Close();
        _character.Root.Visible = visible;
    }

    /// <summary>Raised after the player commits a filter. The screen rebuilds its report.</summary>
    public event Action? Changed;

    /// <summary>
    /// Rebuild the option lists from the archive and re-select what the session was
    /// already filtering on. Called once at construction and again when a background load
    /// finishes, because the ascensions and characters are not known until then.
    /// </summary>
    public void Rebuild()
    {
        var filter = WinrateSession.Filter;
        var ascensions = RunArchive.KnownAscensions();

        // Highest first, so "the one I play" is at the top of the list as well as selected.
        _ascension.SetOptions(
            ascensions
                .Select(ascension => new ComboBox.Option($"A{ascension}", ascension))
                .Prepend(new ComboBox.Option(AllAscensions, null)),
            _ascensionChosen || !RunArchive.HasLoaded
                ? filter.Ascension
                : ascensions.Count > 0 ? ascensions[0] : null);

        _character.SetOptions(
            RunArchive.KnownCharacters()
                .Select(character => new ComboBox.Option(character, character, ArtKey.Character(character)))
                .Prepend(new ComboBox.Option(AllCharacters, null)),
            filter.Character);

        _window.SetOptions(
            Windows.Select(window => new ComboBox.Option(window.Text, window.Days)),
            filter.WindowDays);

        // Only write the selection back once the archive is in. Before then the option
        // lists hold nothing but "All", so publishing would quietly overwrite the
        // remembered ascension with the only value that happens to be selectable yet.
        // Afterwards it is worth doing, because a remembered ascension or character that
        // is no longer in the archive really has fallen back to "All" — and because the
        // ascension picked for the player above has to reach the filter.
        if (RunArchive.HasLoaded)
            Publish();
    }

    /// <summary>Set the character filter from somewhere else — the Home chips do this.</summary>
    public void SelectCharacter(string? character)
    {
        WinrateSession.Filter = WinrateSession.Filter with { Character = character };
        Rebuild();
        Changed?.Invoke();
    }

    private void Publish() =>
        WinrateSession.Filter = new RunFilter
        {
            Ascension = _ascension.Selected as int?,
            Character = _character.Selected as string,
            WindowDays = _window.Selected as int?,
            IgnoreEarlyAbandons = WinrateSettings.IgnoreEarlyAbandons,
        };
}

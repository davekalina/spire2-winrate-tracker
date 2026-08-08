using Godot;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The row that decides which runs the tables cover: ascension, character, and how far back
/// to look. It applies to every tab.
///
/// Options come from the archive itself, so the ascension list only offers ascensions that
/// have actually been played.
/// </summary>
internal sealed class FilterBar
{
    /// <summary>
    /// Windows offered, in order. Null is the whole archive. A user-specified window is
    /// not here yet — it needs a number entry, which this row has no room for.
    /// </summary>
    private static readonly (string Text, int? Days)[] Windows =
    [
        ("All", null),
        ("Last 7 days", 7),
        ("Last 14 days", 14),
        ("Last 30 days", 30),
        ("Last 45 days", 45),
        ("Last 60 days", 60),
        ("Last 90 days", 90),
        ("Last 120 days", 120),
    ];

    private readonly PaginatorRow _row = new();
    private readonly PaginatorRow.Cycler _ascension;
    private readonly PaginatorRow.Cycler _character;
    private readonly PaginatorRow.Cycler _window;

    public FilterBar()
    {
        _ascension = _row.Add("Ascension");
        _character = _row.Add("Character");
        _window = _row.Add("Time window");

        // Publish first: the screen's handler reads the filter this writes.
        _row.Changed += Publish;
        _row.Changed += () => Changed?.Invoke();
        Rebuild();
    }

    public Control Root => _row.Root;

    public List<Control> Controls => _row.Controls;

    public void FocusFirst() => _row.FocusFirst();

    /// <summary>Raised after the player pages any control. The screen rebuilds its report.</summary>
    public event Action? Changed;

    /// <summary>
    /// Rebuild the option lists from the archive and re-select what the session was
    /// already filtering on. Called once at construction and again when a background load
    /// finishes, because the ascensions and characters are not known until then.
    /// </summary>
    public void Rebuild()
    {
        var filter = WinrateSession.Filter;

        _ascension.SetOptions(
            RunArchive.KnownAscensions().Select(ascension => new PaginatorRow.Option($"Ascension {ascension}", ascension)).Prepend(new PaginatorRow.Option("All", null)),
            filter.Ascension);

        _character.SetOptions(
            RunArchive.KnownCharacters().Select(character => new PaginatorRow.Option(character, character)).Prepend(new PaginatorRow.Option("All", null)),
            filter.Character);

        _window.SetOptions(
            Windows.Select(window => new PaginatorRow.Option(window.Text, window.Days)),
            filter.WindowDays);

        // Only write the selection back once the archive is in. Before then the option
        // lists hold nothing but "All", so publishing would quietly overwrite the
        // remembered ascension with the only value that happens to be selectable yet.
        // Afterwards it is worth doing, because a remembered ascension or character that
        // is no longer in the archive really has fallen back to "All".
        if (RunArchive.HasLoaded)
            Publish();
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

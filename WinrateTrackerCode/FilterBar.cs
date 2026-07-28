using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.addons.mega_text;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The row that decides which runs the tables cover.
///
/// All three controls are the game's <c>paginator</c> — the same left/right widget the
/// settings screen uses for Max FPS — rather than a mix of paginators, dropdowns, and
/// tickboxes. One widget for three filters means one focus behaviour, one selection
/// reticle, and one way to read the current value: as text, which is unambiguous in a way
/// a tickbox's state is not.
///
/// Options come from the archive itself, so the ascension list only offers ascensions
/// that have actually been played.
/// </summary>
internal sealed class FilterBar : IDisposable
{
    private const string PaginatorScene = "screens/paginator";

    /// <summary>Natural width of the paginator scene; it must be restated outside its own anchors.</summary>
    private static readonly Vector2 PaginatorSize = new(324, 64);

    private readonly NPaginator _ascension;
    private readonly NPaginator _character;
    private readonly NPaginator _abandoned;

    /// <summary>Filter value per option index. Null means "all".</summary>
    private readonly List<int?> _ascensionValues = [];

    private readonly List<string?> _characterValues = [];

    public FilterBar()
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 48);
        row.Alignment = BoxContainer.AlignmentMode.Center;

        _ascension = AddControl(row, "Ascension");
        _character = AddControl(row, "Character");
        _abandoned = AddControl(row, "Runs");

        Root = row;
        Rebuild();
    }

    public Control Root { get; }

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

        _ascensionValues.Clear();
        _ascensionValues.Add(null);
        _ascensionValues.AddRange(RunArchive.KnownAscensions().Select(ascension => (int?)ascension));
        PaginatorPatch.SetOptions(
            _ascension,
            _ascensionValues.Select(value => value is null ? "All" : $"Ascension {value}"));

        _characterValues.Clear();
        _characterValues.Add(null);
        _characterValues.AddRange(RunArchive.KnownCharacters().Select(character => (string?)character));
        PaginatorPatch.SetOptions(
            _character,
            _characterValues.Select(value => value ?? "All"));

        PaginatorPatch.SetOptions(_abandoned, ["Finished", "With abandoned"]);

        Select(_ascension, IndexOfOrFirst(_ascensionValues, filter.Ascension));
        Select(_character, IndexOfOrFirst(_characterValues, filter.Character));
        Select(_abandoned, filter.IncludeAbandoned ? 1 : 0);

        // Only write the selection back once the archive is in. Before then the option
        // lists hold nothing but "All", so publishing would quietly overwrite the
        // remembered ascension with the only value that happens to be selectable yet.
        // Afterwards it is worth doing, because a remembered ascension or character that
        // is no longer in the archive really has fallen back to "All".
        if (RunArchive.HasLoaded)
            Publish();
    }

    private static int IndexOfOrFirst<T>(List<T> values, T wanted)
    {
        var index = values.IndexOf(wanted);
        return index < 0 ? 0 : index;
    }

    private NPaginator AddControl(Control row, string caption)
    {
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        column.AddThemeConstantOverride("separation", 4);
        column.Alignment = BoxContainer.AlignmentMode.Center;

        var label = NativeStyle.Cell(caption, rightAligned: false, header: true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        column.AddChild(label);

        var paginator = SceneHelper.Instantiate<NPaginator>(PaginatorScene);
        // Taken out of its original screen, so its own anchoring no longer applies and the
        // container needs to be told how much room the arrows and label actually need.
        paginator.CustomMinimumSize = PaginatorSize;
        paginator.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        column.AddChild(paginator);

        PaginatorPatch.Listen(paginator, _ => OnPaged(paginator));
        row.AddChild(column);
        return paginator;
    }

    private void OnPaged(NPaginator paginator)
    {
        RefreshLabel(paginator);
        Publish();
        Changed?.Invoke();
    }

    /// <summary>
    /// Move a paginator without going through the player's paging path, and write the
    /// label ourselves — <c>SetIndex</c> only notifies, it never draws.
    /// </summary>
    private void Select(NPaginator paginator, int index)
    {
        paginator.SetIndex(index);
        RefreshLabel(paginator);
    }

    private static void RefreshLabel(NPaginator paginator)
    {
        var selected = PaginatorPatch.OptionAt(paginator, PaginatorPatch.IndexOf(paginator));
        paginator.GetNode<MegaLabel>("%Label").SetTextAutoSize(selected);
    }

    private void Publish() =>
        WinrateSession.Filter = new RunFilter
        {
            Ascension = ValueAt(_ascensionValues, PaginatorPatch.IndexOf(_ascension)),
            Character = ValueAt(_characterValues, PaginatorPatch.IndexOf(_character)),
            IncludeAbandoned = PaginatorPatch.IndexOf(_abandoned) == 1,
        };

    private static T? ValueAt<T>(List<T?> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : default;

    public void Dispose()
    {
        PaginatorPatch.Forget(_ascension);
        PaginatorPatch.Forget(_character);
        PaginatorPatch.Forget(_abandoned);
    }
}

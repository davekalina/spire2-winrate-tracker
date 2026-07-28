using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.addons.mega_text;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The row that decides which runs the tables cover.
///
/// All three controls are the game's <c>paginator</c> scene — the left/right widget the
/// settings screen uses for Max FPS. One widget for three filters means one focus
/// behaviour, one hover animation, and one way to read the current value: as text, which
/// is unambiguous in a way a tickbox's state is not.
///
/// The scene is driven directly rather than through <c>NPaginator</c>. Its root node
/// carries no script — the settings screen attaches one per paginator in its own scene —
/// so instantiating it yields a plain <see cref="Control" /> and there is no
/// <c>NPaginator</c> to talk to. What the scene does provide is the two arrows, which are
/// real <c>NPaginateArrow</c> buttons with their own hover and press feedback. They look
/// for a paginator parent with a null-conditional call, find none, and do nothing — but
/// they still emit <c>Released</c>, which is all this needs. Paging, the label, and the
/// wrap-around are handled here.
///
/// Options come from the archive itself, so the ascension list only offers ascensions
/// that have actually been played.
/// </summary>
internal sealed class FilterBar
{
    private const string PaginatorScene = "screens/paginator";

    /// <summary>Natural size of the paginator scene; it must be restated outside its own anchors.</summary>
    private static readonly Vector2 PaginatorSize = new(324, 64);

    private readonly Cycler _ascension;
    private readonly Cycler _character;
    private readonly Cycler _abandoned;

    public FilterBar()
    {
        var row = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        row.AddThemeConstantOverride("separation", 48);

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

        _ascension.SetOptions(
            RunArchive.KnownAscensions().Select(ascension => new Option($"Ascension {ascension}", ascension)).Prepend(new Option("All", null)),
            filter.Ascension);

        _character.SetOptions(
            RunArchive.KnownCharacters().Select(character => new Option(character, character)).Prepend(new Option("All", null)),
            filter.Character);

        _abandoned.SetOptions(
            [new Option("Finished", false), new Option("With abandoned", true)],
            filter.IncludeAbandoned);

        // Only write the selection back once the archive is in. Before then the option
        // lists hold nothing but "All", so publishing would quietly overwrite the
        // remembered ascension with the only value that happens to be selectable yet.
        // Afterwards it is worth doing, because a remembered ascension or character that
        // is no longer in the archive really has fallen back to "All".
        if (RunArchive.HasLoaded)
            Publish();
    }

    private Cycler AddControl(Control row, string caption)
    {
        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        column.AddThemeConstantOverride("separation", 4);

        var captionLabel = NativeStyle.Cell(caption, rightAligned: false, header: true);
        captionLabel.HorizontalAlignment = HorizontalAlignment.Center;
        captionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        column.AddChild(captionLabel);

        var paginator = SceneHelper.Instantiate<Control>(PaginatorScene);
        // Taken out of its original screen, so its own anchoring no longer applies and the
        // container has to be told how much room the arrows and label actually need.
        paginator.CustomMinimumSize = PaginatorSize;
        paginator.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        column.AddChild(paginator);
        row.AddChild(column);

        var cycler = new Cycler(paginator);
        // Publish first: the screen's handler reads the filter this writes.
        cycler.Changed += Publish;
        cycler.Changed += () => Changed?.Invoke();
        return cycler;
    }

    private void Publish() =>
        WinrateSession.Filter = new RunFilter
        {
            Ascension = _ascension.Selected as int?,
            Character = _character.Selected as string,
            IncludeAbandoned = _abandoned.Selected is true,
        };

    /// <summary>One option: what it reads as, and the filter value behind it.</summary>
    private readonly record struct Option(string Text, object? Value);

    /// <summary>
    /// One paginator: its label, its options, and which way the arrows move through them.
    /// </summary>
    private sealed class Cycler
    {
        private readonly MegaLabel _label;
        private readonly List<Option> _options = [];
        private int _index;

        public Cycler(Control paginator)
        {
            _label = paginator.GetNode<MegaLabel>("%Label");
            // The scene's second label exists only to animate the outgoing value during
            // NPaginator's page tween. Nothing here runs that tween, so it would sit on
            // screen showing the scene's placeholder text.
            paginator.GetNode<Control>("%VfxLabel").Visible = false;

            Connect(paginator, "LeftArrow", -1);
            Connect(paginator, "RightArrow", +1);
        }

        public event Action? Changed;

        public object? Selected => _index >= 0 && _index < _options.Count ? _options[_index].Value : null;

        public void SetOptions(IEnumerable<Option> options, object? selected)
        {
            _options.Clear();
            _options.AddRange(options);

            var index = _options.FindIndex(option => Equals(option.Value, selected));
            _index = index < 0 ? 0 : index;
            Refresh();
        }

        private void Connect(Control paginator, string arrowName, int step)
        {
            if (paginator.GetNodeOrNull<NClickableControl>(arrowName) is not { } arrow)
                return;
            arrow.Connect(
                NClickableControl.SignalName.Released,
                Callable.From<NClickableControl>(_ => Step(step)));
        }

        private void Step(int step)
        {
            if (_options.Count <= 1)
                return;
            _index = (_index + step + _options.Count) % _options.Count;
            Refresh();
            Changed?.Invoke();
        }

        private void Refresh() =>
            _label.SetTextAutoSize(_index >= 0 && _index < _options.Count ? _options[_index].Text : "");
    }
}

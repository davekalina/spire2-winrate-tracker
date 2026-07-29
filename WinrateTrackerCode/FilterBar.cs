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
/// <c>NPaginator</c> to talk to.
///
/// Its two arrows cannot be reused as they are either: <c>NPaginateArrow._Ready</c> opens
/// with <c>GetParent&lt;NPaginator&gt;()</c>, which is a hard cast, so under any other
/// parent it throws before reaching the three lines that bind its own image and shader.
/// The arrow is then half-built and dead. So each arrow's artwork is moved onto a control
/// this file owns, and the scripted node is dropped — before the scene enters the tree,
/// where <c>_Ready</c> would fire. The texture, shader, scale, and pivot all travel with
/// the reparented image, so the arrows still look exactly like the settings screen's.
///
/// Options come from the archive itself, so the ascension list only offers ascensions
/// that have actually been played.
/// </summary>
internal sealed class FilterBar
{
    private const string PaginatorScene = "screens/paginator";

    /// <summary>Natural size of the paginator scene; it must be restated outside its own anchors.</summary>
    private static readonly Vector2 PaginatorSize = new(324, 64);

    /// <summary>
    /// Windows offered, in order. Null is the whole archive. A user-specified window is
    /// not here yet — it needs a number entry, which this row has no room for.
    /// </summary>
    private static readonly (string Text, int? Days)[] Windows =
    [
        ("All", null),
        ("Last 30 days", 30),
        ("Last 60 days", 60),
        ("Last 90 days", 90),
    ];

    private readonly Cycler _ascension;
    private readonly Cycler _character;
    private readonly Cycler _window;

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
        _window = AddControl(row, "Time window");

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

        _window.SetOptions(
            Windows.Select(window => new Option(window.Text, window.Days)),
            filter.WindowDays);

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
            WindowDays = _window.Selected as int?,
            IgnoreEarlyAbandons = WinrateSettings.IgnoreEarlyAbandons,
        };

    /// <summary>One option: what it reads as, and the filter value behind it.</summary>
    private readonly record struct Option(string Text, object? Value);

    /// <summary>
    /// One paginator: its label, its options, and which way the arrows move through them.
    /// </summary>
    private sealed class Cycler
    {
        /// <summary>Hover nudge, taken from <c>NPaginateArrow.OnFocus</c>.</summary>
        private const float HoverScale = 1.1f;

        private const double HoverDuration = 0.05;

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

            ReplaceArrow(paginator, "LeftArrow", -1);
            ReplaceArrow(paginator, "RightArrow", +1);
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

        /// <summary>
        /// Swap one <c>NPaginateArrow</c> for a plain control wearing its artwork, and
        /// make that clickable. Called before the paginator is in the tree, so the
        /// scripted node is freed without its <c>_Ready</c> ever running.
        /// </summary>
        private void ReplaceArrow(Control paginator, string arrowName, int step)
        {
            if (paginator.GetNodeOrNull<Control>(arrowName) is not { } scripted)
                return;
            if (scripted.GetNodeOrNull<TextureRect>("Image") is not { } image)
                return;

            var arrow = new Control
            {
                Name = arrowName,
                CustomMinimumSize = scripted.CustomMinimumSize,
                AnchorLeft = scripted.AnchorLeft,
                AnchorTop = scripted.AnchorTop,
                AnchorRight = scripted.AnchorRight,
                AnchorBottom = scripted.AnchorBottom,
                OffsetLeft = scripted.OffsetLeft,
                OffsetTop = scripted.OffsetTop,
                OffsetRight = scripted.OffsetRight,
                OffsetBottom = scripted.OffsetBottom,
                GrowHorizontal = scripted.GrowHorizontal,
                GrowVertical = scripted.GrowVertical,
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            };

            scripted.RemoveChild(image);
            arrow.AddChild(image);
            paginator.RemoveChild(scripted);
            scripted.QueueFree();
            paginator.AddChild(arrow);

            var restingScale = image.Scale;
            arrow.Connect(Control.SignalName.MouseEntered, Callable.From(() => Scale(image, restingScale * HoverScale)));
            arrow.Connect(Control.SignalName.MouseExited, Callable.From(() => Scale(image, restingScale)));
            arrow.Connect(
                Control.SignalName.GuiInput,
                Callable.From<InputEvent>(input =>
                {
                    if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                        return;
                    arrow.AcceptEvent();
                    Scale(image, restingScale);
                    Step(step);
                }));
        }

        /// <summary>Matches the scale nudge <c>NPaginateArrow</c> plays on hover.</summary>
        private static void Scale(Control image, Vector2 target)
        {
            if (!image.IsInsideTree())
                return;
            image.CreateTween().TweenProperty(image, "scale", target, HoverDuration);
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

using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
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
/// Its two arrows cannot be reused as they are either: the arrow script's <c>_Ready</c> opens
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
        ("Last 7 days", 7),
        ("Last 14 days", 14),
        ("Last 30 days", 30),
        ("Last 45 days", 45),
        ("Last 60 days", 60),
        ("Last 90 days", 90),
        ("Last 120 days", 120),
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
        LinkFocus();
        Rebuild();
    }

    public Control Root { get; }

    /// <summary>
    /// Left and right step between the filters; up and down are pinned to self so the
    /// d-pad cannot wander out of the row into the scrolling tables.
    /// </summary>
    private void LinkFocus()
    {
        for (var i = 0; i < Controls.Count; i++)
        {
            var control = Controls[i];
            control.FocusNeighborLeft = (i > 0 ? Controls[i - 1] : Controls[i]).GetPath();
            control.FocusNeighborRight = (i < Controls.Count - 1 ? Controls[i + 1] : Controls[i]).GetPath();
            control.FocusNeighborTop = control.GetPath();
            control.FocusNeighborBottom = control.GetPath();
            control.FocusNext = control.GetPath();
            control.FocusPrevious = control.GetPath();
        }
    }

    /// <summary>Put focus on a filter, so the bumpers have something to page.</summary>
    public void FocusFirst() => Controls.FirstOrDefault()?.TryGrabFocus();

    /// <summary>
    /// The three paginators, left to right. The screen wires focus neighbours through
    /// them so a gamepad can reach the filters and leave again.
    /// </summary>
    public List<Control> Controls { get; } = [];

    /// <summary>Raised after the player pages any control. The screen rebuilds its report.</summary>
    public event Action? Changed;

    /// <summary>
    /// Page whichever filter currently has focus, for the bumpers. Falls back to the first
    /// filter so a bumper press does something sensible before anything has been focused.
    /// </summary>
    public void StepFocused(int step)
    {
        var focused = Root.GetViewport()?.GuiGetFocusOwner();
        var index = Controls.FindIndex(control => control == focused);
        if (index < 0)
        {
            index = 0;
            Controls.ElementAtOrDefault(0)?.TryGrabFocus();
        }
        _cyclers.ElementAtOrDefault(index)?.Step(step);
    }

    private readonly List<Cycler> _cyclers = [];

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
        // The scene already sets focus_mode = All; restated because everything about this
        // control being reachable on a gamepad depends on it.
        paginator.FocusMode = Control.FocusModeEnum.All;
        column.AddChild(paginator);
        row.AddChild(column);
        Controls.Add(paginator);

        var cycler = new Cycler(paginator);
        // Publish first: the screen's handler reads the filter this writes.
        cycler.Changed += Publish;
        cycler.Changed += () => Changed?.Invoke();
        _cyclers.Add(cycler);
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
        /// <summary>Hover nudge, taken from the game's own paginator arrow.</summary>
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
            WireControllerInput(paginator);
        }

        /// <summary>
        /// Focus drives the scene's selection reticle, which is otherwise dormant here
        /// because nothing runs it without <c>NPaginator</c>.
        ///
        /// Paging is deliberately <em>not</em> wired to left and right: on this screen the
        /// d-pad moves between the three filters, and the bumpers change the focused one's
        /// value. See <see cref="FilterBar.StepFocused" />.
        /// </summary>
        private void WireControllerInput(Control paginator)
        {
            var reticle = paginator.GetNodeOrNull<NSelectionReticle>("SelectionReticle");

            paginator.Connect(Control.SignalName.FocusEntered, Callable.From(() => reticle?.OnSelect()));
            paginator.Connect(Control.SignalName.FocusExited, Callable.From(() => reticle?.OnDeselect()));

            // A click anywhere on the widget takes focus, so mouse and gamepad agree on
            // which filter is live.
            paginator.MouseFilter = Control.MouseFilterEnum.Pass;
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
        /// Swap one scripted arrow for a plain control wearing its artwork, and
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
                // v0.110 moved the arrows into their own scene, where the image is drawn
                // with use_parent_material — it takes its shader from the arrow, not from
                // itself. Reparenting the image onto a bare Control would drop that
                // shader, so the arrow's material comes across too.
                Material = scripted.Material,
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

        /// <summary>Matches the scale nudge the game's arrow plays on hover.</summary>
        private static void Scale(Control image, Vector2 target)
        {
            if (!image.IsInsideTree())
                return;
            image.CreateTween().TweenProperty(image, "scale", target, HoverDuration);
        }

        public void Step(int step)
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

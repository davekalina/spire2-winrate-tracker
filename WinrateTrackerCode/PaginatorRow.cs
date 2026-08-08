using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.addons.mega_text;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// A row of the game's <c>paginator</c> widgets — the left/right control the settings
/// screen uses for Max FPS — each under a caption.
///
/// One widget for every filter on this screen means one focus behaviour, one hover
/// animation, and one way to read the current value: as text, which is unambiguous in a way
/// a tickbox's state is not.
///
/// The scene is driven directly rather than through <c>NPaginator</c>. Its root node carries
/// no script — the settings screen attaches one per paginator in its own scene — so
/// instantiating it yields a plain <see cref="Control" /> and there is no <c>NPaginator</c>
/// to talk to.
///
/// Each arrow is its own focus stop, so a gamepad selects an arrow and presses it the same
/// way a mouse clicks it. The widget as a whole is not focusable — focusing the pair rather
/// than a button would leave nothing for Select to press.
///
/// Its two arrows cannot be reused as they are either: the arrow script's <c>_Ready</c> opens
/// with <c>GetParent&lt;NPaginator&gt;()</c>, which is a hard cast, so under any other
/// parent it throws before reaching the three lines that bind its own image and shader.
/// The arrow is then half-built and dead. So each arrow's artwork is moved onto a control
/// this file owns, and the scripted node is dropped — before the scene enters the tree,
/// where <c>_Ready</c> would fire. The texture, shader, scale, and pivot all travel with
/// the reparented image, so the arrows still look exactly like the settings screen's.
/// </summary>
internal sealed class PaginatorRow
{
    private const string PaginatorScene = "screens/paginator";

    /// <summary>Natural size of the paginator scene; it must be restated outside its own anchors.</summary>
    private static readonly Vector2 PaginatorSize = new(324, 64);

    private readonly HBoxContainer _row;

    public PaginatorRow(int separation = 48)
    {
        _row = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        _row.AddThemeConstantOverride("separation", separation);
        Root = _row;
    }

    public Control Root { get; }

    /// <summary>
    /// Every arrow in the row, left to right. These are the row's only focus stops; the
    /// screen chains a gamepad's cursor through them.
    /// </summary>
    public List<Control> Controls { get; } = [];

    /// <summary>Raised after the player pages any widget in the row.</summary>
    public event Action? Changed;

    /// <summary>Put the cursor on the first arrow.</summary>
    public void FocusFirst() => Controls.FirstOrDefault()?.TryGrabFocus();

    public Cycler Add(string caption)
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
        // The scene sets focus_mode = All on the widget. The arrows are the buttons here,
        // so they take the focus and the widget itself must give it up — otherwise the
        // cursor lands on the pair and Select has nothing to press.
        paginator.FocusMode = Control.FocusModeEnum.None;
        column.AddChild(paginator);
        _row.AddChild(column);

        var cycler = new Cycler(paginator);
        Controls.AddRange(cycler.Arrows);
        cycler.Changed += () => Changed?.Invoke();
        return cycler;
    }

    /// <summary>One option: what it reads as, and the filter value behind it.</summary>
    public readonly record struct Option(string Text, object? Value);

    /// <summary>
    /// One paginator: its label, its options, and which way the arrows move through them.
    /// </summary>
    public sealed class Cycler
    {
        /// <summary>Hover nudge, taken from the game's own paginator arrow.</summary>
        private const float HoverScale = 1.1f;

        private const double HoverDuration = 0.05;

        /// <summary>The scene's own highlight, instanced once per arrow.</summary>
        private const string ReticleScene = "ui/selection_reticle";

        private readonly MegaLabel _label;
        private readonly List<Option> _options = [];
        private int _index;

        public Cycler(Control paginator)
        {
            _label = paginator.GetNode<MegaLabel>("%Label");
            // The scene's reticle is anchored to the whole widget, so lighting it would
            // bracket the label and both arrows at once — which says nothing about which
            // button the cursor is on. Each arrow gets its own instead.
            if (paginator.GetNodeOrNull<Control>("SelectionReticle") is { } shared)
                shared.Visible = false;
            // The scene's second label exists only to animate the outgoing value during
            // NPaginator's page tween. Nothing here runs that tween, so it would sit on
            // screen showing the scene's placeholder text.
            paginator.GetNode<Control>("%VfxLabel").Visible = false;

            ReplaceArrow(paginator, "LeftArrow", -1);
            ReplaceArrow(paginator, "RightArrow", +1);

            // Clicks land on the arrows, not on the widget behind them.
            paginator.MouseFilter = Control.MouseFilterEnum.Pass;
        }

        /// <summary>Both arrows, left then right. These are the widget's focus stops.</summary>
        public List<Control> Arrows { get; } = [];

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
                // The arrow is the button, so it is what the cursor lands on and what
                // Select presses.
                FocusMode = Control.FocusModeEnum.All,
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

            // The game's own selection bracket, sized to this arrow alone. Anchors and
            // offsets are both written out: the scene carries offsets for the place it sits
            // in combat (-129, -179 to 123, -1), and a preset call that sets anchors alone
            // keeps those and stretches the brackets right across the screen.
            var reticle = SceneHelper.Instantiate<NSelectionReticle>(ReticleScene);
            reticle.AnchorLeft = 0f;
            reticle.AnchorTop = 0f;
            reticle.AnchorRight = 1f;
            reticle.AnchorBottom = 1f;
            reticle.OffsetLeft = 0f;
            reticle.OffsetTop = 0f;
            reticle.OffsetRight = 0f;
            reticle.OffsetBottom = 0f;
            reticle.GrowHorizontal = Control.GrowDirection.Both;
            reticle.GrowVertical = Control.GrowDirection.Both;
            reticle.MouseFilter = Control.MouseFilterEnum.Ignore;
            arrow.AddChild(reticle);

            arrow.Connect(Control.SignalName.FocusEntered, Callable.From(() =>
            {
                // _Ready sets the pivot from a size the reticle does not have yet, so it
                // would scale out of one corner. By now the row has been laid out.
                reticle.PivotOffset = reticle.Size * 0.5f;
                reticle.OnSelect();
                Scale(image, restingScale * HoverScale);
            }));
            arrow.Connect(Control.SignalName.FocusExited, Callable.From(() =>
            {
                reticle.OnDeselect();
                Scale(image, restingScale);
            }));

            arrow.Connect(
                Control.SignalName.GuiInput,
                Callable.From<InputEvent>(input =>
                {
                    // Mouse press, or Select while focused — the two ways to press a button
                    // on this screen. NClickableControl does the same for the game's own.
                    var pressed = input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
                        || input.IsActionPressed(MegaInput.select);
                    if (!pressed)
                        return;
                    arrow.AcceptEvent();
                    Step(step);
                }));

            Arrows.Add(arrow);
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

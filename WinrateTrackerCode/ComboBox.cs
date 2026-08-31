using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.addons.mega_text;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// One filter, as a labelled box with a drop-down list.
///
/// This replaced a pair of <c>paginator</c> arrows per filter, and the reason is the
/// gamepad. A paginator is two focus stops — one per arrow — so five filters were ten
/// stops, and reaching the last one meant pressing right nine times while the value under
/// each arrow changed as you passed it. A combo is <em>one</em> stop that opens a list, so
/// the same five filters are five presses and nothing changes until something is chosen.
/// It also fits every filter on one row, which is what let the second row on the Cards and
/// Relics tabs go away.
///
/// Both halves are built rather than borrowed. The game has no drop-down of its own to
/// duplicate — its settings screens page through values with those same arrows — so the
/// box is dressed in the screen's own materials instead: the tab plates' raised fill and
/// border when open, the popup skin shared with <see cref="HoverTip" /> for the list, and
/// the paginator's gold for the triangle.
///
/// While a list is open it takes over cancel the way <see cref="ModalPanel" /> does. Without
/// that, B on a pad would dismiss the whole screen from under the open list.
/// </summary>
internal sealed class ComboBox
{
    /// <summary>
    /// The row's measurements, a step down from the design's.
    ///
    /// The pick tabs carry five filters and a search field on one row, which the design
    /// never had to fit. Everything here gives up two or three pixels so the field has
    /// somewhere to be; at these sizes the row still reads as the same furniture.
    /// </summary>
    public const float Height = 44f;

    private const int PaddingX = 13;
    private const int Separation = 8;
    private const float IconSize = 26f;
    private const int CaptionFontSize = 17;
    private const int ValueFontSize = 20;

    private const float ListMinWidth = 260f;
    private const float ListMaxHeight = 520f;
    private const float ListGap = 12f;
    private const int ListPaddingY = 6;

    private const float OptionHeight = 46f;
    private const int OptionPaddingX = 20;
    private const int OptionFontSize = 22;
    private const float OptionIconSize = 32f;

    /// <summary>What the back button listens for, and therefore what an open list must take over.</summary>
    private static readonly string[] CancelHotkeys =
        [MegaInput.cancel, MegaInput.pauseAndBack, MegaInput.back];

    /// <summary>One option: what it reads as, the art beside it, and the filter value behind it.</summary>
    public readonly record struct Option(string Text, object? Value, string? Icon = null);

    private readonly Control _host;
    private readonly PanelContainer _button;
    private readonly MegaLabel _value;
    private readonly Control _iconSlot;
    private readonly List<Option> _options = [];

    private Control? _list;
    private Control? _backdrop;
    private Action? _cancel;
    private int _index;

    /// <summary>
    /// <paramref name="host" /> is what the drop-down list is parented to — the screen, not
    /// the filter row. A list parented to the row would be clipped by it and drawn under the
    /// tables; parented to the screen it hangs over everything, which is what a list is for.
    ///
    /// <paramref name="caption" /> may be empty, and should be wherever the values say what
    /// they are on their own: "All Characters" and "Ironclad" need no word "Character" in
    /// front of them, and the row has better uses for the width. A caption earns its place
    /// only where the value alone is ambiguous — "10" needs "Ascension".
    /// </summary>
    public ComboBox(Control host, string caption)
    {
        _host = host;

        _button = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, Height),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            FocusMode = Control.FocusModeEnum.All,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        _button.AddThemeStyleboxOverride("panel", ButtonBox(open: false));

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", Separation);

        if (caption.Length > 0)
        {
            var captionLabel = NativeStyle.Figure(caption, CaptionFontSize, NativeStyle.ColumnHeaderColor);
            captionLabel.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(captionLabel);
        }

        _iconSlot = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            Visible = false,
        };
        row.AddChild(_iconSlot);

        _value = NativeStyle.Figure("", ValueFontSize, NativeStyle.CellColor);
        _value.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_value);
        row.AddChild(Triangle());

        var inset = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        inset.AddThemeConstantOverride("margin_left", PaddingX);
        inset.AddThemeConstantOverride("margin_right", PaddingX);
        inset.AddChild(row);
        _button.AddChild(inset);

        _button.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(OnButtonInput));
        _button.Connect(Control.SignalName.FocusEntered, Callable.From(() => Emphasise(true)));
        _button.Connect(Control.SignalName.FocusExited, Callable.From(() => Emphasise(IsOpen)));
        _button.Connect(Control.SignalName.MouseEntered, Callable.From(() => Emphasise(true)));
        _button.Connect(Control.SignalName.MouseExited, Callable.From(() => Emphasise(IsOpen || _button.HasFocus())));
        // A combo left open when its screen is torn down would leave an orphan list drawn
        // over the Compendium with nothing able to dismiss it.
        _button.Connect(Node.SignalName.TreeExiting, Callable.From(Close));
    }

    /// <summary>The control the focus chain walks to. One per filter, which is the whole point.</summary>
    public Control Root => _button;

    /// <summary>Raised when the player commits a different option.</summary>
    public event Action? Changed;

    public object? Selected => _index >= 0 && _index < _options.Count ? _options[_index].Value : null;

    public bool IsOpen => _list is not null && _list.IsValid();

    /// <summary>
    /// Refill the list and re-select <paramref name="selected" />, falling back to the first
    /// option when what was selected is no longer offered — a character who has dropped out
    /// of the archive really has fallen back to "All".
    /// </summary>
    public void SetOptions(IEnumerable<Option> options, object? selected)
    {
        Close();
        _options.Clear();
        _options.AddRange(options);

        var index = _options.FindIndex(option => Equals(option.Value, selected));
        _index = index < 0 ? 0 : index;
        Refresh();
    }

    private void Refresh()
    {
        if (_index < 0 || _index >= _options.Count)
        {
            _value.SetTextAutoSize("");
            _iconSlot.Visible = false;
            return;
        }

        var option = _options[_index];
        _value.SetTextAutoSize(option.Text);

        foreach (var child in _iconSlot.GetChildren())
        {
            _iconSlot.RemoveChild(child);
            child.QueueFree();
        }

        if (GameArt.Icon(option.Icon, IconSize) is { } icon)
        {
            _iconSlot.CustomMinimumSize = new Vector2(IconSize, IconSize);
            _iconSlot.AddChild(icon);
            icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _iconSlot.Visible = true;
        }
        else
        {
            _iconSlot.Visible = false;
        }
    }

    // ── the box ──────────────────────────────────────────────────────────────

    private static StyleBoxFlat ButtonBox(bool open) => new()
    {
        BgColor = open ? NativeStyle.RaisedTopColor : new Color(1f, 1f, 1f, 0.04f),
        BorderColor = open ? NativeStyle.FocusColor : NativeStyle.BorderColor,
        BorderWidthLeft = 2,
        BorderWidthTop = 2,
        BorderWidthRight = 2,
        BorderWidthBottom = 2,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
        ShadowColor = open ? NativeStyle.FocusColor with { A = 0.4f } : new Color(0, 0, 0, 0),
        ShadowSize = open ? 14 : 0,
    };

    /// <summary>
    /// Light the box up for hover, focus, or being open. One appearance for all three: on a
    /// pad, focus is the cursor, and it has to be as obvious as a mouse hover.
    /// </summary>
    private void Emphasise(bool lit)
    {
        if (!_button.IsValid())
            return;
        _button.AddThemeStyleboxOverride("panel", ButtonBox(lit));
    }

    /// <summary>
    /// The drop triangle, in the paginator arrow's gold.
    ///
    /// A <see cref="Polygon2D" /> because a mod cannot attach a script to run <c>_Draw</c>,
    /// and a triangle is not a rectangle. It is a <see cref="Node2D" /> under a
    /// <see cref="Control" />, so it draws in the control's own space and needs no layout of
    /// its own — only a sized parent for the row to lay out.
    /// </summary>
    private static Control Triangle()
    {
        var slot = new Control
        {
            CustomMinimumSize = new Vector2(TriangleWidth, TriangleHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        slot.AddChild(new Polygon2D
        {
            Polygon =
            [
                new Vector2(0, 0),
                new Vector2(TriangleWidth, 0),
                new Vector2(TriangleWidth / 2f, TriangleHeight),
            ],
            Color = NativeStyle.ArrowColor,
        });
        return slot;
    }

    private const float TriangleWidth = 16f;
    private const float TriangleHeight = 10f;

    private void OnButtonInput(InputEvent input)
    {
        var pressed = input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
            || input.IsActionPressed(MegaInput.select)
            || input.IsActionPressed(MegaInput.confirm);
        if (!pressed)
            return;

        _button.AcceptEvent();
        if (IsOpen)
            Close();
        else
            Open();
    }

    // ── the list ─────────────────────────────────────────────────────────────

    public void Open()
    {
        if (IsOpen || _options.Count == 0 || !_button.IsInsideTree() || !_host.IsInsideTree())
            return;

        // Clicking anywhere else closes the list. People try this before they find the
        // control again, so it has to work — the same rule ModalPanel follows.
        _backdrop = new Control { MouseFilter = Control.MouseFilterEnum.Stop, ZIndex = 40 };
        _backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _backdrop.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(input =>
        {
            if (input is InputEventMouseButton { Pressed: true })
                Close();
        }));
        _host.AddChild(_backdrop);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, ZIndex = 41 };
        panel.AddThemeStyleboxOverride("panel", NativeStyle.PopupBox());

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 0);

        var rows = new List<Control>(_options.Count);
        for (var i = 0; i < _options.Count; i++)
            rows.Add(BuildOption(i, column));

        scroll.AddChild(column);
        var inset = new MarginContainer();
        inset.AddThemeConstantOverride("margin_top", ListPaddingY);
        inset.AddThemeConstantOverride("margin_bottom", ListPaddingY);
        inset.AddChild(scroll);
        panel.AddChild(inset);
        _host.AddChild(panel);
        _list = panel;

        Place(panel, rows.Count);
        ChainOptions(rows);
        Emphasise(true);

        // B / Escape closes the list rather than the screen underneath it, for exactly as
        // long as the list is up.
        _cancel = Close;
        foreach (var hotkey in CancelHotkeys)
            NHotkeyManager.Instance?.PushHotkeyReleasedBinding(hotkey, _cancel);

        // The cursor opens on what is already selected, so a pad can see where it is in the
        // list before it starts moving.
        rows.ElementAtOrDefault(_index)?.TryGrabFocus();
    }

    private Control BuildOption(int index, Control column)
    {
        var option = _options[index];
        var chosen = index == _index;

        var row = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, OptionHeight),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            FocusMode = Control.FocusModeEnum.All,
        };
        row.AddThemeStyleboxOverride("panel", OptionBox(chosen, focused: false));

        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 12);

        if (GameArt.Of(option.Icon) is not null)
            line.AddChild(GameArt.IconSlot(option.Icon, OptionIconSize));

        var label = NativeStyle.Figure(
            option.Text,
            OptionFontSize,
            chosen ? NativeStyle.HeaderColor : NativeStyle.CellColor);
        label.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(label);

        if (chosen)
        {
            line.AddChild(HoverTip.Spacer());
            var mark = NativeStyle.Figure("●", OptionFontSize - 2, NativeStyle.HeaderColor);
            mark.VerticalAlignment = VerticalAlignment.Center;
            line.AddChild(mark);
        }

        var inset = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        inset.AddThemeConstantOverride("margin_left", OptionPaddingX);
        inset.AddThemeConstantOverride("margin_right", OptionPaddingX);
        inset.AddChild(line);
        row.AddChild(inset);

        void Light(bool lit) => row.AddThemeStyleboxOverride("panel", OptionBox(chosen, lit));

        row.Connect(Control.SignalName.MouseEntered, Callable.From(() => Light(true)));
        row.Connect(Control.SignalName.MouseExited, Callable.From(() => Light(row.HasFocus())));
        row.Connect(Control.SignalName.FocusEntered, Callable.From(() => Light(true)));
        row.Connect(Control.SignalName.FocusExited, Callable.From(() => Light(false)));
        row.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(input =>
        {
            var pressed = input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
                || input.IsActionPressed(MegaInput.select)
                || input.IsActionPressed(MegaInput.confirm);
            if (!pressed)
                return;
            row.AcceptEvent();
            Commit(index);
        }));

        column.AddChild(row);
        return row;
    }

    private static StyleBoxFlat OptionBox(bool chosen, bool focused) => new()
    {
        BgColor = focused
            ? NativeStyle.FocusColor with { A = 0.26f }
            : chosen
                ? NativeStyle.FocusColor with { A = 0.14f }
                : new Color(0, 0, 0, 0),
    };

    /// <summary>
    /// Up and down walk the list and stop at its ends rather than wrapping. Left and right
    /// are pinned to the row itself so the cursor cannot step sideways out of an open list
    /// into the filter row behind it, and tab is pinned for the same reason.
    /// </summary>
    private static void ChainOptions(List<Control> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].FocusNeighborTop = (i > 0 ? rows[i - 1] : rows[i]).GetPath();
            rows[i].FocusNeighborBottom = (i < rows.Count - 1 ? rows[i + 1] : rows[i]).GetPath();
            rows[i].FocusNeighborLeft = rows[i].GetPath();
            rows[i].FocusNeighborRight = rows[i].GetPath();
            rows[i].FocusNext = rows[i].GetPath();
            rows[i].FocusPrevious = rows[i].GetPath();
        }
    }

    /// <summary>
    /// Hang the list under its box, clamped to the screen. Global rects are differenced
    /// because the list is parented to the screen and the box to the filter row, which share
    /// no coordinate space.
    /// </summary>
    private void Place(Control panel, int optionCount)
    {
        var buttonRect = _button.GetGlobalRect();
        var hostRect = _host.GetGlobalRect();

        var width = Math.Max(ListMinWidth, Math.Max(buttonRect.Size.X, panel.GetCombinedMinimumSize().X));
        var height = Math.Min(ListMaxHeight, (optionCount * OptionHeight) + (ListPaddingY * 2) + ListBorder);
        panel.Size = new Vector2(width, height);

        var left = buttonRect.Position.X - hostRect.Position.X;
        var top = buttonRect.Position.Y - hostRect.Position.Y + buttonRect.Size.Y + ListGap;
        panel.Position = new Vector2(
            Math.Clamp(left, 0f, Math.Max(0f, hostRect.Size.X - width)),
            Math.Clamp(top, 0f, Math.Max(0f, hostRect.Size.Y - height)));
    }

    /// <summary>The popup skin's two-pixel edge, top and bottom.</summary>
    private const float ListBorder = 4f;

    private void Commit(int index)
    {
        var changed = index != _index;
        _index = index;
        Refresh();
        Close();
        if (changed)
            Changed?.Invoke();
    }

    /// <summary>
    /// Close without committing, and hand the cursor back to the box.
    ///
    /// Returning focus is not a nicety: the option rows are about to be freed, and a pad
    /// left pointing at a freed node has nothing to move from.
    /// </summary>
    public void Close()
    {
        if (_cancel is not null)
        {
            foreach (var hotkey in CancelHotkeys)
                NHotkeyManager.Instance?.RemoveHotkeyReleasedBinding(hotkey, _cancel);
            _cancel = null;
        }

        var wasOpen = IsOpen;

        if (_list is not null && _list.IsValid())
            _list.QueueFree();
        _list = null;

        if (_backdrop is not null && _backdrop.IsValid())
            _backdrop.QueueFree();
        _backdrop = null;

        Emphasise(_button.IsValid() && _button.HasFocus());
        if (wasOpen && _button.IsValid() && _button.IsInsideTree())
            _button.TryGrabFocus();
    }
}

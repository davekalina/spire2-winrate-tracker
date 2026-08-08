using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.addons.mega_text;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The Statistics screen's look, as values.
///
/// Every colour, size, and spacing below is read off
/// <c>res://scenes/screens/stats_screen/stats_screen.tscn</c> and the game's shared
/// <see cref="StsColors" />, so this screen sits beside the native one without looking
/// like a different program. Fonts are the game's own Kreon variations, loaded from
/// <c>res://themes/</c> rather than approximated.
///
/// Labels are <see cref="MegaLabel" /> — the game's own label type — with auto-sizing
/// off, because table cells have to keep a common baseline size to stay in columns.
/// </summary>
internal static class NativeStyle
{
    private const string BoldFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private const string RegularFontPath = "res://themes/kreon_regular_glyph_space_one.tres";

    /// <summary>Section headings, from <c>OverallStatsHeader</c>.</summary>
    public const int HeaderFontSize = 36;

    public const int HeaderHeight = 64;
    public const int ColumnHeaderFontSize = 22;
    public const int CellFontSize = 25;
    public const int NoteFontSize = 20;

    /// <summary>
    /// Gap between columns. The native stats screen uses 56, but it only ever shows two
    /// columns; at eight or more that much air is what pushes a table past the screen.
    /// </summary>
    public const int ColumnSeparation = 34;

    /// <summary>
    /// Gap between the parts of one cell — a record and its own rate. Deliberately far
    /// tighter than <see cref="ColumnSeparation" />: they belong to each other, and a
    /// column-sized gap makes them look as unrelated as two different characters.
    /// </summary>
    public const int PartSeparation = 12;

    public const int RowSeparation = 6;
    public const int SectionSeparation = 28;

    /// <summary>Panel insets, from <c>OverallStatsDetails</c>.</summary>
    public const int PanelPaddingLeft = 24;

    public const int PanelPaddingRight = 24;
    public const int PanelPaddingTop = 12;
    public const int PanelPaddingBottom = 12;

    /// <summary>Header gold, from <c>theme_override_colors/font_color</c>.</summary>
    public static readonly Color HeaderColor = new(0.937255f, 0.784314f, 0.317647f, 1f);

    public static readonly Color HeaderOutlineColor = new(0.3f, 0.23f, 0.132f, 1f);
    public static readonly Color HeaderShadowColor = new(0f, 0f, 0f, 0.25098f);

    /// <summary>The translucent slate behind a stats block, from its <c>ColorRect</c>.</summary>
    public static readonly Color PanelColor = new(0.1159f, 0.16777f, 0.19f, 0.501961f);

    /// <summary>
    /// The band behind the filter rows, which is the same slate but solid.
    ///
    /// It cannot be translucent like <see cref="PanelColor" />. A table panel is
    /// translucent over the menu art behind the screen, which is fine; this band has the
    /// scrolling tables passing underneath it, and at half alpha they read straight
    /// through the filters. The screen has no background of its own to tint against — it
    /// draws over whatever the menu is showing — so the only way to stop the overdraw is
    /// to be opaque.
    ///
    /// The value is the panel slate laid over darkness and lifted a little, so the band
    /// still reads as the same material as the panels below rather than as a black bar.
    /// </summary>
    public static readonly Color HeaderBandColor = new(0.072f, 0.104f, 0.118f, 1f);

    public static readonly Color CellColor = StsColors.cream;
    public static readonly Color ColumnHeaderColor = StsColors.halfTransparentCream;
    public static readonly Color NoteColor = StsColors.halfTransparentCream;

    private static Font? _bold;
    private static Font? _regular;

    private static Font Bold => _bold = Reload(_bold, BoldFontPath);
    private static Font Regular => _regular = Reload(_regular, RegularFontPath);

    /// <summary>
    /// Fetch a font, re-loading it if the one held has been freed underneath us.
    ///
    /// A plain <c>??=</c> cache is not safe here. Godot's resource cache releases a font
    /// once nothing in the tree references it, which happens every time the game tears a
    /// scene down — entering or leaving a run. The managed wrapper survives that, but its
    /// native object does not, and the next <c>AddThemeFontOverride</c> throws
    /// <see cref="ObjectDisposedException" />. That reads as the screen working for a
    /// session and then quietly building nothing but empty tabs.
    /// </summary>
    private static Font Reload(Font? held, string path) =>
        held is not null && GodotObject.IsInstanceValid(held) ? held : GD.Load<Font>(path);

    /// <summary>A gold section heading, matching the native "Overall Stats" header exactly.</summary>
    public static MegaLabel Header(string text)
    {
        var label = Label(text, HeaderFontSize, HeaderColor, bold: true);
        label.CustomMinimumSize = new Vector2(0, HeaderHeight);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeColorOverride("font_outline_color", HeaderOutlineColor);
        label.AddThemeColorOverride("font_shadow_color", HeaderShadowColor);
        label.AddThemeConstantOverride("outline_size", 8);
        label.AddThemeConstantOverride("shadow_offset_x", 8);
        label.AddThemeConstantOverride("shadow_offset_y", 6);
        label.AddThemeConstantOverride("shadow_outline_size", 0);
        return label;
    }

    /// <summary>A caveat under a table, in the game's dimmed cream.</summary>
    public static MegaLabel Note(string text)
    {
        var label = Label(text, NoteFontSize, NoteColor, bold: false);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    /// <summary>
    /// A table cell. Numeric cells expand and right-align so their digits meet a common
    /// right edge; label cells hug their text on the left.
    /// </summary>
    public static MegaLabel Cell(string text, bool rightAligned, bool header = false)
    {
        var label = Label(
            text,
            header ? ColumnHeaderFontSize : CellFontSize,
            header ? ColumnHeaderColor : CellColor,
            bold: header);
        label.HorizontalAlignment = rightAligned ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        label.SizeFlagsHorizontal = rightAligned ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill;
        return label;
    }

    /// <summary>
    /// Auto-sizing is off on every label here. <see cref="MegaLabel" /> shrinks text to
    /// fit its box, which would let one long cell set a different size from its
    /// neighbours and break the row's baseline.
    /// </summary>
    private static MegaLabel Label(string text, int fontSize, Color color, bool bold)
    {
        var label = new MegaLabel
        {
            AutoSizeEnabled = false,
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontOverride("font", bold ? Bold : Regular);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>
    /// A small labelled button, built from the game's settings tab — the one native
    /// scene that is a self-contained, focusable button carrying a label it will set for
    /// you.
    ///
    /// The scene sizes itself for a tab row and draws its label at 32 px. Shrinking the
    /// frame alone leaves the text overflowing it, because the label is anchored to the
    /// frame and MegaLabel will not shrink text below the scene's own floor. So the font
    /// is stepped down with the frame.
    /// </summary>
    public static Control TextButton(string text, Action onPressed)
    {
        var button = SceneHelper.Instantiate<NSettingsTab>("screens/settings_tab");
        button.CustomMinimumSize = ButtonSize;
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        // NClickableControl only treats a control as controller-navigable when its focus
        // mode is All, and the scene does not set one.
        button.FocusMode = Control.FocusModeEnum.All;
        button.Ready += () =>
        {
            if (button.GetNodeOrNull<MegaLabel>("Label") is { } label)
            {
                label.MinFontSize = ButtonFontSize;
                label.MaxFontSize = ButtonFontSize;
                label.AddThemeFontSizeOverride("font_size", ButtonFontSize);
            }
            button.SetLabel(text);
            // Tabs open deselected, which reads as "off" on a plain button.
            button.Select();
        };
        button.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>(_ => onPressed()));
        return button;
    }

    /// <summary>Wide enough for "Show Graph" at <see cref="ButtonFontSize" />.</summary>
    private static readonly Vector2 ButtonSize = new(224, 64);

    private const int ButtonFontSize = 22;

    /// <summary>
    /// A bare icon button, the way the game presents its own gear: the top bar's settings
    /// button is a 64 px icon with no frame behind it. A settings tab makes a poor frame
    /// here — its texture is 256x90, so squeezed into a square it draws as a thin bar with
    /// the icon spilling out of it.
    /// </summary>
    public static Control IconButton(string texturePath, float size, Action onPressed)
    {
        var button = new Control
        {
            CustomMinimumSize = new Vector2(size, size),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            FocusMode = Control.FocusModeEnum.All,
        };

        var icon = new TextureRect
        {
            Texture = GD.Load<Texture2D>(texturePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            PivotOffset = new Vector2(size / 2f, size / 2f),
            Modulate = IconRestColor,
        };
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(icon);

        void Highlight() { icon.Modulate = Colors.White; Nudge(icon, Vector2.One * IconHoverScale); }
        void Rest() { icon.Modulate = IconRestColor; Nudge(icon, Vector2.One); }

        button.Connect(Control.SignalName.MouseEntered, Callable.From(Highlight));
        button.Connect(Control.SignalName.MouseExited, Callable.From(Rest));
        // The same emphasis for controller focus as for the mouse, or the gamepad has no
        // way to tell the gear is the thing it is pointing at.
        button.Connect(Control.SignalName.FocusEntered, Callable.From(Highlight));
        button.Connect(Control.SignalName.FocusExited, Callable.From(Rest));

        button.Connect(
            Control.SignalName.GuiInput,
            Callable.From<InputEvent>(input =>
            {
                var pressed = input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
                    || input.IsActionPressed(MegaInput.confirm)
                    || input.IsActionPressed(MegaInput.select);
                if (!pressed)
                    return;
                button.AcceptEvent();
                onPressed();
            }));
        return button;
    }

    private static void Nudge(Control icon, Vector2 target)
    {
        if (icon.IsInsideTree())
            icon.CreateTween().TweenProperty(icon, "scale", target, 0.05);
    }

    private const float IconHoverScale = 1.12f;

    private static readonly Color IconRestColor = new(1f, 1f, 1f, 0.78f);

    /// <summary>
    /// The mod's byline. Two lines and small, the way Hypergeo signs its shelf: the name,
    /// then the version and author under it.
    /// </summary>
    public static MegaLabel Byline(string name, string version, string author)
    {
        var label = Label($"{name}\n{version} by {author}", BylineFontSize, HeaderColor, bold: true);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        return label;
    }

    private const int BylineFontSize = 17;

    /// <summary>How wide a cell's text is in the body font. Drives part alignment.</summary>
    public static float MeasureCell(string text) =>
        string.IsNullOrEmpty(text)
            ? 0f
            : Regular.GetStringSize(text, HorizontalAlignment.Left, -1, CellFontSize).X;

    /// <summary>The translucent panel a table sits on, with the native insets applied.</summary>
    public static MarginContainer Panel(Control content)
    {
        var panel = new MarginContainer();
        panel.AddChild(new ColorRect { Color = PanelColor, MouseFilter = Control.MouseFilterEnum.Ignore });

        var inset = new MarginContainer();
        inset.AddThemeConstantOverride("margin_left", PanelPaddingLeft);
        inset.AddThemeConstantOverride("margin_right", PanelPaddingRight);
        inset.AddThemeConstantOverride("margin_top", PanelPaddingTop);
        inset.AddThemeConstantOverride("margin_bottom", PanelPaddingBottom);
        inset.AddChild(content);
        panel.AddChild(inset);
        return panel;
    }
}

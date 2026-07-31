using Godot;
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
    /// A square icon button carrying one of the game's own textures. Built on the same
    /// settings tab as <see cref="TextButton" /> so it hovers and focuses identically,
    /// with the label left empty and the icon laid over it.
    /// </summary>
    public static Control IconButton(string texturePath, float size, Action onPressed)
    {
        var button = SceneHelper.Instantiate<NSettingsTab>("screens/settings_tab");
        button.CustomMinimumSize = new Vector2(size, size);
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        button.Ready += () =>
        {
            button.SetLabel("");
            button.Select();
        };

        var icon = new TextureRect
        {
            Texture = GD.Load<Texture2D>(texturePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        icon.OffsetLeft = IconInset;
        icon.OffsetTop = IconInset;
        icon.OffsetRight = -IconInset;
        icon.OffsetBottom = -IconInset;
        button.AddChild(icon);

        button.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>(_ => onPressed()));
        return button;
    }

    private const float IconInset = 14f;

    /// <summary>The mod's byline, in the dimmed cream used for secondary text.</summary>
    public static MegaLabel Byline(string text)
    {
        var label = Label(text, NoteFontSize, NoteColor, bold: false);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        return label;
    }

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

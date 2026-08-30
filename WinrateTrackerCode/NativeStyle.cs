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

    /// <summary>
    /// The same Kreon bold, spaced two glyph units apart instead of one.
    ///
    /// Godot's <see cref="Label" /> has no tracking property, so letter-spacing has to come
    /// from the font: a <c>FontVariation</c> with a <c>spacing_glyph</c>. The game already
    /// ships the variations — this is the route its own theme takes — so the small-caps
    /// headings here are set the way the game sets its own rather than by hand.
    /// </summary>
    private const string CapsFontPath = "res://themes/kreon_bold_glyph_space_two.tres";

    /// <summary>Section headings, from <c>OverallStatsHeader</c>.</summary>
    public const int HeaderFontSize = 36;

    public const int HeaderHeight = 58;
    public const int ColumnHeaderFontSize = 20;
    public const int CellFontSize = 25;
    public const int NoteFontSize = 20;

    /// <summary>Panel captions — <c>LAST 50 RUNS</c> — on Home.</summary>
    public const int CaptionFontSize = 21;

    /// <summary>The rate on the Home headline. The one figure the screen exists to show.</summary>
    public const int HeroFontSize = 118;

    /// <summary>A stat box's figure.</summary>
    public const int StatFontSize = 44;

    /// <summary>A row of text. Rows carrying art need <see cref="ArtRowHeight" /> instead.</summary>
    public const int RowHeight = 33;

    public const int ArtRowHeight = 40;

    /// <summary>Gap under a column heading. There is no rule; the first stripe is the rule.</summary>
    public const int HeaderRowGap = 8;

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

    /// <summary>
    /// The selection cyan, sampled from the tab plate's own glow. It marks one thing on this
    /// screen: what is open or selected right now.
    /// </summary>
    public static readonly Color FocusColor = new(0.380392f, 0.776471f, 0.823529f, 1f);

    /// <summary>
    /// The slate a popup sits on — the filter band lifted, because a drop-down list hangs
    /// over the tables and has to be read against them rather than through them.
    /// </summary>
    public static readonly Color PopupColor = new(0.086275f, 0.137255f, 0.164706f, 1f);

    /// <summary>The raised fill on an open combo box, from the tab plates' own plate.</summary>
    public static readonly Color RaisedTopColor = new(0.192157f, 0.282353f, 0.352941f, 1f);

    /// <summary>The resting border of a combo, matching the tab plates' own edge.</summary>
    public static readonly Color BorderColor = new(0.470588f, 0.588235f, 0.650980f, 0.45f);

    /// <summary>The paginator arrow's gold, kept for the combo's drop triangle.</summary>
    public static readonly Color ArrowColor = new(0.941176f, 0.760784f, 0.298039f, 1f);

    /// <summary>Chart blue: a quantity that is not a win rate and so has no good side.</summary>
    public static readonly Color MeasuredColor = new(0.549020f, 0.850980f, 0.925490f, 1f);

    public static readonly Color BarFillColor = new(0.549020f, 0.850980f, 0.925490f, 0.85f);

    /// <summary>A win, and anything better than what it is measured against.</summary>
    public static readonly Color GoodColor = new(0.498039f, 0.831373f, 0.549020f, 1f);

    /// <summary>A loss, and anything worse.</summary>
    public static readonly Color BadColor = new(0.878431f, 0.552941f, 0.431373f, 1f);

    /// <summary>Between the two: not bad, not good. Dimmer than the header gold on purpose.</summary>
    public static readonly Color MiddlingColor = new(0.843137f, 0.635294f, 0.290196f, 1f);

    /// <summary>
    /// The alternating row wash. Barely there: it exists to keep the eye on one row across
    /// eight columns, not to draw a grid.
    /// </summary>
    public static readonly Color ZebraColor = new(1f, 1f, 1f, 0.045f);

    /// <summary>The empty part of a comparison bar's track.</summary>
    public static readonly Color TrackColor = new(1f, 1f, 1f, 0.06f);

    /// <summary>A loss pip's fill, which is an absence rather than a colour.</summary>
    public static readonly Color EmptyPipColor = new(1f, 1f, 1f, 0.05f);

    private static Font? _bold;
    private static Font? _regular;
    private static Font? _caps;

    private static Font Bold => _bold = Reload(_bold, BoldFontPath);
    private static Font Regular => _regular = Reload(_regular, RegularFontPath);
    private static Font Caps => _caps = Reload(_caps, CapsFontPath);

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

    /// <summary>
    /// A column heading: small, dim, and in caps with the glyph spacing opened up.
    ///
    /// Caps rather than a rule or a background. A heading has to read as a different kind of
    /// thing from the numbers under it, and at eight columns a gold rule under every table
    /// is more furniture than the tables can carry — the first zebra stripe already draws
    /// the line between head and body.
    /// </summary>
    public static MegaLabel ColumnHeader(string text, bool rightAligned)
    {
        var label = new MegaLabel
        {
            AutoSizeEnabled = false,
            Text = text.ToUpperInvariant(),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = rightAligned ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        label.AddThemeFontOverride("font", Caps);
        label.AddThemeFontSizeOverride("font_size", ColumnHeaderFontSize);
        label.AddThemeColorOverride("font_color", ColumnHeaderColor);
        return label;
    }

    /// <summary>A panel's small caps caption — <c>LAST 50 RUNS</c>, <c>THIS MONTH · AUG</c>.</summary>
    public static MegaLabel Caption(string text, int fontSize = CaptionFontSize)
    {
        var label = new MegaLabel
        {
            AutoSizeEnabled = false,
            Text = text.ToUpperInvariant(),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontOverride("font", Caps);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", ColumnHeaderColor);
        return label;
    }

    /// <summary>A figure at any size, in whatever the caller has decided it means.</summary>
    public static MegaLabel Figure(string text, int fontSize, Color color, bool bold = true)
    {
        var label = Label(text, fontSize, color, bold);
        label.VerticalAlignment = VerticalAlignment.Bottom;
        return label;
    }

    /// <summary>
    /// The Home headline: the rate, at the size of the thing the screen is for. Carries the
    /// section header's outline and drop shadow so it reads as the same material as the gold
    /// headings rather than as oversized body text.
    /// </summary>
    public static MegaLabel Hero(string text)
    {
        var label = Label(text, HeroFontSize, HeaderColor, bold: true);
        label.VerticalAlignment = VerticalAlignment.Bottom;
        label.AddThemeColorOverride("font_outline_color", HeaderOutlineColor);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.3f));
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeConstantOverride("shadow_offset_x", 8);
        label.AddThemeConstantOverride("shadow_offset_y", 6);
        label.AddThemeConstantOverride("shadow_outline_size", 0);
        return label;
    }

    /// <summary>What a <see cref="Tone" /> is drawn in.</summary>
    public static Color ToneColor(Tone tone) => tone switch
    {
        Tone.Good => GoodColor,
        Tone.Bad => BadColor,
        Tone.Measured => MeasuredColor,
        Tone.Quiet => ColumnHeaderColor,
        _ => HeaderColor,
    };

    /// <summary>
    /// What colour a rate is, judged against the rate it is being compared with.
    ///
    /// The bands are multiples of the player's own average rather than fixed percentages.
    /// A fixed "green above 33%" would tell a player who wins six runs in ten that a 30%
    /// month was good, which is the opposite of what the column is for.
    /// </summary>
    public static Color RateColor(double rate, double baseline)
    {
        if (baseline <= 0d)
            return rate > 0d ? GoodColor : BadColor;
        var ratio = rate / baseline;
        return ratio >= 1.3d ? GoodColor
            : ratio >= 0.95d ? HeaderColor
            : ratio >= 0.6d ? MiddlingColor
            : BadColor;
    }

    /// <summary>How wide a cell's text is in the body font. Drives part alignment.</summary>
    public static float MeasureCell(string text) =>
        string.IsNullOrEmpty(text)
            ? 0f
            : Regular.GetStringSize(text, HorizontalAlignment.Left, -1, CellFontSize).X;

    /// <summary>
    /// How wide a string is at a given size. Used to wrap prose by hand: Godot's autowrap
    /// reports a minimum width of nothing and a height computed from whatever width the
    /// label happens to have, which inside a container that has not been laid out yet is
    /// zero — one character per line, and a panel sized for it.
    /// </summary>
    public static float Measure(string text, int fontSize, bool bold) =>
        string.IsNullOrEmpty(text)
            ? 0f
            : (bold ? Bold : Regular).GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;

    /// <summary>The same, for a column heading — a different font at a different size.</summary>
    public static float MeasureColumnHeader(string text) =>
        string.IsNullOrEmpty(text)
            ? 0f
            : Caps.GetStringSize(text.ToUpperInvariant(), HorizontalAlignment.Left, -1, ColumnHeaderFontSize).X;

    /// <summary>
    /// A win or a loss as one square.
    ///
    /// A win is filled and carries its letter; a loss is an outline. That asymmetry is the
    /// point — a strip of ten reads as "how many are lit" at a glance, which two equally
    /// solid colours would not.
    /// </summary>
    public static Control Pip(bool win, float width, float height, bool lettered)
    {
        var pip = new Panel { CustomMinimumSize = new Vector2(width, height) };
        pip.MouseFilter = Control.MouseFilterEnum.Ignore;
        pip.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        pip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = win ? GoodColor : EmptyPipColor,
            BorderColor = win ? GoodColor : new Color(StsColors.cream, 0.24f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        });

        if (!lettered)
            return pip;

        // Dark ink on the filled pip, dim cream on the empty one: in both cases the letter
        // is the quieter half of the mark and the fill is what is read first.
        var letter = Label(win ? "W" : "L", PipFontSize, win ? PipInkColor : ColumnHeaderColor, bold: true);
        letter.HorizontalAlignment = HorizontalAlignment.Center;
        letter.VerticalAlignment = VerticalAlignment.Center;
        letter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        pip.AddChild(letter);
        return pip;
    }

    private const int PipFontSize = 19;

    private static readonly Color PipInkColor = new(0.054902f, 0.101961f, 0.078431f, 1f);

    /// <summary>
    /// A vertical two-stop fill, for the trend's bars.
    ///
    /// A <see cref="ColorRect" /> is one flat colour and a <see cref="StyleBoxFlat" /> has no
    /// gradient, so the gradient has to be an actual texture. It is one pixel wide and
    /// stretched, which costs nothing.
    /// </summary>
    public static TextureRect Gradient(Color top, Color bottom) =>
        new()
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Godot.Gradient { Offsets = [0f, 1f], Colors = [top, bottom] },
                Width = 1,
                Height = 64,
                FillFrom = Vector2.Zero,
                FillTo = new Vector2(0, 1),
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

    /// <summary>
    /// The skin every popup on this screen wears: the lifted slate, a cyan edge, and a
    /// shadow deep enough to lift it off the tables underneath. Shared so the drop-down
    /// lists and the hover tips cannot drift apart into two different-looking widgets.
    /// </summary>
    public static StyleBoxFlat PopupBox() => new()
    {
        BgColor = PopupColor,
        BorderColor = FocusColor,
        BorderWidthLeft = 2,
        BorderWidthTop = 2,
        BorderWidthRight = 2,
        BorderWidthBottom = 2,
        ShadowColor = new Color(0, 0, 0, 0.65f),
        ShadowSize = 14,
        ShadowOffset = new Vector2(0, 8),
    };

    /// <summary>The translucent panel a table sits on, with the native insets applied.</summary>
    public static MarginContainer Panel(Control content) =>
        Panel(content, PanelPaddingLeft, PanelPaddingTop, PanelPaddingRight, PanelPaddingBottom);

    /// <summary>The same slate at whatever inset the contents need.</summary>
    public static MarginContainer Panel(Control content, int left, int top, int right, int bottom)
    {
        var panel = new MarginContainer();
        panel.AddChild(new ColorRect { Color = PanelColor, MouseFilter = Control.MouseFilterEnum.Ignore });

        var inset = new MarginContainer();
        inset.AddThemeConstantOverride("margin_left", left);
        inset.AddThemeConstantOverride("margin_right", right);
        inset.AddThemeConstantOverride("margin_top", top);
        inset.AddThemeConstantOverride("margin_bottom", bottom);
        inset.AddChild(content);
        panel.AddChild(inset);
        return panel;
    }

    /// <summary>
    /// One row's comparison bar: a track, a fill, and a notch at the rate the fill is being
    /// judged against.
    ///
    /// Everything is placed with anchors rather than pixel positions, so the bar re-lays
    /// itself out at whatever width the column ends up with and there is no resize handler
    /// to forget to connect.
    /// </summary>
    public static Control ComparisonBar(double? value, BarSpec spec)
    {
        var bar = new Control
        {
            CustomMinimumSize = new Vector2(0, BarHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };

        var track = new ColorRect { Color = TrackColor, MouseFilter = Control.MouseFilterEnum.Ignore };
        track.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bar.AddChild(track);

        if (value is { } rate)
        {
            var notch = Fraction(spec.Baseline, spec.Scale);
            var here = Fraction(rate, spec.Scale);
            // Unsigned bars grow from zero and are read by length. Signed ones grow out of
            // the notch, so which side they fall on is the reading.
            var from = spec.Signed ? Math.Min(notch, here) : 0f;
            var to = spec.Signed ? Math.Max(notch, here) : here;

            var fill = new ColorRect
            {
                Color = FillColor(rate, spec),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = from,
                AnchorRight = to,
                AnchorTop = 0f,
                AnchorBottom = 1f,
            };
            bar.AddChild(fill);
        }

        // Drawn last so it sits over the fill: the notch is the thing being compared
        // against and has to stay legible when the fill reaches it.
        var baseline = new ColorRect
        {
            Color = ColumnHeaderColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = Fraction(spec.Baseline, spec.Scale),
            AnchorRight = Fraction(spec.Baseline, spec.Scale),
            AnchorTop = 0f,
            AnchorBottom = 1f,
            OffsetLeft = -1f,
            OffsetRight = 1f,
            OffsetTop = -NotchOverhang,
            OffsetBottom = NotchOverhang,
        };
        bar.AddChild(baseline);
        return bar;
    }

    private static float Fraction(double value, double scale) =>
        scale <= 0d ? 0f : (float)Math.Clamp(value / scale, 0d, 1d);

    private static Color FillColor(double rate, BarSpec spec)
    {
        if (!spec.Signed)
            return RateColor(rate, spec.Baseline);
        // A pick above the player's own rate is gold, and green once it is winning more
        // often than not — which is a claim worth telling apart from "better than me".
        if (rate < spec.Baseline)
            return BadColor;
        return rate >= 0.5d ? GoodColor : HeaderColor;
    }

    private const int BarHeight = 14;
    private const float NotchOverhang = 3f;
}

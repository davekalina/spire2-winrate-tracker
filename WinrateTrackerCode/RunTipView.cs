using Godot;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// One run, read out: who, at what ascension, when, how long, and how it ended.
///
/// Three places on this screen draw a strip of win/loss pips — the Home headline's last
/// ten, each character chip's, and the Characters table's last-ten column — and a pip is a
/// coloured square that says almost nothing on its own. All three hand their pips to this,
/// so pointing at one always tells you the same things about the run behind it.
///
/// The ascension is badged over the character's own icon, the way the game badges it on
/// the score screen, rather than spelled out in a line of its own.
/// </summary>
internal static class RunTipView
{
    /// <summary>Wide enough for the longest "Killed by …" the archive holds.</summary>
    public const float Width = 440f;

    private const float PortraitSize = 52f;
    private const float BadgeSize = 34f;

    /// <summary>How far the ascension badge hangs off the portrait's corner.</summary>
    private const float BadgeOverhang = 8f;

    private const int BadgeFontSize = 18;
    private const float ClockSize = 26f;
    private const int NameFontSize = 27;
    private const int DetailFontSize = 21;

    public static Control Build(RunSummary run) =>
        HoverTip.Column(
            HoverTip.Row(
                14,
                Portrait(run),
                HoverTip.Line(run.Character, NativeStyle.CellColor, NameFontSize, bold: true),
                HoverTip.Spacer(),
                HoverTip.Line(
                    run.Win ? "WIN" : "LOSS",
                    run.Win ? NativeStyle.GoodColor : NativeStyle.BadColor,
                    DetailFontSize,
                    bold: true)),
            HoverTip.Row(16, HoverTip.Line(run.When, NativeStyle.CellColor), Length(run)),
            HoverTip.Line(run.Outcome, NativeStyle.CellColor with { A = 0.85f }),
            HoverTip.Line(run.Detail, NativeStyle.ColumnHeaderColor, DetailFontSize));

    /// <summary>The character's icon with the ascension flame over its bottom-right corner.</summary>
    private static Control Portrait(RunSummary run)
    {
        var portrait = new Control
        {
            CustomMinimumSize = new Vector2(PortraitSize, PortraitSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };

        if (GameArt.Icon(ArtKey.Character(run.Character), PortraitSize) is { } icon)
        {
            icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            portrait.AddChild(icon);
        }

        portrait.AddChild(Badge(run.Ascension));
        return portrait;
    }

    private static Control Badge(int ascension)
    {
        var badge = new Control
        {
            CustomMinimumSize = new Vector2(BadgeSize, BadgeSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 1f,
            AnchorTop = 1f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -BadgeSize + BadgeOverhang,
            OffsetTop = -BadgeSize + BadgeOverhang,
            OffsetRight = BadgeOverhang,
            OffsetBottom = BadgeOverhang,
        };

        if (GameArt.Icon(ArtKey.Ascension, BadgeSize) is { } flame)
        {
            flame.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            badge.AddChild(flame);
        }

        var number = NativeStyle.Figure(Format.Count(ascension), BadgeFontSize, NativeStyle.CellColor);
        number.HorizontalAlignment = HorizontalAlignment.Center;
        number.VerticalAlignment = VerticalAlignment.Center;
        number.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        // The flame is bright and the number sits on top of it.
        number.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
        number.AddThemeConstantOverride("shadow_offset_x", 0);
        number.AddThemeConstantOverride("shadow_offset_y", 2);
        badge.AddChild(number);
        return badge;
    }

    private static Control Length(RunSummary run)
    {
        var length = HoverTip.Row(
            8,
            GameArt.IconSlot(ArtKey.Clock, ClockSize),
            HoverTip.Line(run.Length, NativeStyle.CellColor));
        length.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return length;
    }

    /// <summary>
    /// Make a pip show its run while the cursor is over it.
    ///
    /// The pips are small and sit shoulder to shoulder, so the tip is pinned under the strip
    /// rather than following the cursor: a tip that slid along as you crossed ten adjacent
    /// targets would be one that never settles anywhere long enough to read.
    /// </summary>
    public static void Attach(HoverTip? tip, Control pip, RunSummary run) =>
        tip?.Attach(pip, () => Build(run), Width);
}

using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.addons.mega_text;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The Home tab, drawn.
///
/// Three rows, in the order the questions get asked: how am I doing lately, which
/// character is carrying it, and where does that sit against the month, the patch, and
/// how far my runs are getting.
///
/// Everything here is placed rather than tabulated, because none of it is a table. What
/// each figure <em>says</em> is decided in <see cref="HomePanel" />, which is compiled
/// without Godot and covered by tests; this file only decides where it goes and what
/// colour it is.
/// </summary>
internal sealed class HomeView
{
    private const int RowGap = 20;

    /// <summary>
    /// Gap between panels sharing a row. The design draws 20 in a 1420-wide column; the
    /// screen's column is narrower once the scrollbar is allowed for, and five character
    /// chips across it are the tightest thing on the tab.
    /// </summary>
    private const int PanelGap = 16;

    /// <summary>
    /// How the headline and the trend divide the row. The design draws them at 620 and 780
    /// in a 1420 column; as a ratio they keep those proportions at whatever width the
    /// content column actually gets, which is not 1420 at every resolution.
    /// </summary>
    private const float HeadlineRatio = 620f;

    private const float TrendRatio = 780f;

    private const float PlotHeight = 186f;

    /// <summary>Room to the right of the plot for the shared percentage axis.</summary>
    private const float AxisGutter = 58f;

    private const int GridLines = 5;
    private const int BarGap = 10;
    private const float BarCapHeight = 3f;

    /// <summary>A run pip on the headline. Big enough to carry its letter and be hovered.</summary>
    private const float RunPipSize = 26f;

    private const int RunPipGap = 5;
    private const float ChipIconSize = 36f;

    /// <summary>Five chips share the row, so their padding is the row's tightest constraint.</summary>
    private const int ChipPaddingX = 12;
    private const float ChipPipHeight = 9f;
    private const int ChipPipGap = 4;

    private const float RunTipWidth = 440f;
    private const float BarTipWidth = 300f;
    private const float TrendTipWidth = 470f;

    private const int NameFontSize = 21;
    private const int ChipRecordFontSize = 32;
    private const int ChipFiftyFontSize = 19;
    private const int DeltaFontSize = 25;
    private const int BaselineFontSize = 18;
    private const int LegendFontSize = 18;
    private const int AxisFontSize = 17;
    private const int DetailFontSize = 19;
    private const int SmallFontSize = 17;

    private readonly HoverTip _tip;
    private readonly Action<string?> _onCharacter;

    /// <param name="onCharacter">
    /// Sets the screen's character filter. Pressing the selected chip passes null, which
    /// clears it — the chip is the filter, so pressing it again is how you let go of it.
    /// </param>
    public HomeView(HoverTip tip, Action<string?> onCharacter)
    {
        _tip = tip;
        _onCharacter = onCharacter;
    }

    /// <summary>The character chips, which are focus stops the screen chains into its rows.</summary>
    public List<Control> Controls { get; } = [];

    /// <summary>
    /// Let go of the chips, which the screen does whenever it leaves Home.
    ///
    /// Not tidiness. The chips are freed with the rest of the tab, and a freed node's
    /// managed wrapper throws <see cref="ObjectDisposedException" /> when anything asks it
    /// to compare itself — which is what a stale list gets asked to do the moment the next
    /// tab is drawn.
    /// </summary>
    public void Clear() => Controls.Clear();

    /// <summary>
    /// Which chip was last pressed, so the cursor can be put back on it.
    ///
    /// Pressing a chip re-filters the screen, which redraws the tab, which frees the very
    /// chip that was pressed — leaving a gamepad with nothing focused and no way to tell
    /// where it was. The chips are built from the archive ignoring the character filter, so
    /// their order does not change when one is chosen and the index still means the same
    /// character afterwards.
    /// </summary>
    private int _pressed = -1;

    /// <summary>Put the cursor back on the chip that was pressed. Call after the redraw.</summary>
    public void RestoreFocus()
    {
        if (_pressed >= 0 && _pressed < Controls.Count && Controls[_pressed].IsValid())
            Controls[_pressed].TryGrabFocus();
        _pressed = -1;
    }

    public Control Build(HomePanel panel, string emptyMessage)
    {
        Controls.Clear();

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", RowGap);

        if (panel == HomePanel.Empty)
        {
            column.AddChild(NativeStyle.Header(emptyMessage));
            return column;
        }

        column.AddChild(TopRow(panel));
        if (panel.Characters.Count > 0)
            column.AddChild(CharacterRow(panel));
        if (panel.Stats.Count > 0)
            column.AddChild(StatRow(panel));
        return column;
    }

    // ── row one: the headline and the trend ──────────────────────────────────

    private Control TopRow(HomePanel panel)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", PanelGap);

        var headline = Headline(panel);
        headline.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        headline.SizeFlagsStretchRatio = HeadlineRatio;
        row.AddChild(headline);

        var trend = panel.Trend is null ? new Control() : Trend(panel.Trend);
        trend.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        trend.SizeFlagsStretchRatio = TrendRatio;
        row.AddChild(trend);
        return row;
    }

    private Control Headline(HomePanel panel)
    {
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        column.AddThemeConstantOverride("separation", 4);

        column.AddChild(HoverTip.Row(
            12,
            NativeStyle.Caption(panel.RecentCaption),
            HoverTip.Spacer(),
            NativeStyle.Figure(panel.RecentRecord, DetailFontSize, NativeStyle.ColumnHeaderColor)));

        // The rate, then what it is worth knowing against, stacked beside it so the eye
        // lands on the figure first and the qualification second.
        var beside = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        beside.AddThemeConstantOverride("separation", 0);
        beside.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        if (panel.RecentDelta.Length > 0)
            beside.AddChild(NativeStyle.Figure(
                panel.RecentDelta, DeltaFontSize, NativeStyle.ToneColor(panel.RecentDeltaTone)));
        beside.AddChild(NativeStyle.Figure(
            panel.RecentBaseline, BaselineFontSize, NativeStyle.ColumnHeaderColor, bold: false));

        var rate = HoverTip.Row(PanelGap, NativeStyle.Hero(panel.RecentRate), beside);
        rate.Alignment = BoxContainer.AlignmentMode.Begin;
        column.AddChild(rate);

        if (panel.RecentRuns.Count > 0)
            column.AddChild(RunStrip(panel.RecentRuns));

        return NativeStyle.Panel(column, 22, 18, 22, 20);
    }

    /// <summary>
    /// The last ten runs as pips, oldest on the left. Each one is hoverable and reads out
    /// the whole run, which is the only place on the screen a single run is visible at all.
    /// </summary>
    private Control RunStrip(IReadOnlyList<HomeRun> runs)
    {
        var strip = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        strip.AddThemeConstantOverride("separation", RunPipGap);

        foreach (var run in runs)
        {
            var pip = NativeStyle.Pip(run.Win, RunPipSize, RunPipSize, lettered: true);
            var remembered = run;
            _tip.Attach(pip, () => RunTip(remembered), RunTipWidth);
            strip.AddChild(pip);
        }

        var row = HoverTip.Row(14, NativeStyle.Caption("Last 10"), strip);
        row.MouseFilter = Control.MouseFilterEnum.Pass;

        var spaced = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        spaced.AddThemeConstantOverride("margin_top", 14);
        spaced.AddChild(row);
        return spaced;
    }

    /// <summary>
    /// One run, read out: who, at what ascension, when, how long, and how it ended. The
    /// ascension is badged over the character's own icon the way the game badges it on the
    /// score screen, rather than spelled out in a line of its own.
    /// </summary>
    private static Control RunTip(HomeRun run)
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
        var ascension = NativeStyle.Figure(Format.Count(run.Ascension), BadgeFontSize, NativeStyle.CellColor);
        ascension.HorizontalAlignment = HorizontalAlignment.Center;
        ascension.VerticalAlignment = VerticalAlignment.Center;
        ascension.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        ascension.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
        ascension.AddThemeConstantOverride("shadow_offset_x", 0);
        ascension.AddThemeConstantOverride("shadow_offset_y", 2);
        badge.AddChild(ascension);
        portrait.AddChild(badge);

        var length = HoverTip.Row(
            8,
            GameArt.IconSlot(ArtKey.Clock, ClockSize),
            HoverTip.Line(run.Length, NativeStyle.CellColor));
        length.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        return HoverTip.Column(
            HoverTip.Row(
                14,
                portrait,
                HoverTip.Line(run.Character, NativeStyle.CellColor, 27, bold: true),
                HoverTip.Spacer(),
                HoverTip.Line(
                    run.Win ? "WIN" : "LOSS",
                    run.Win ? NativeStyle.GoodColor : NativeStyle.BadColor,
                    BaselineFontSize,
                    bold: true)),
            HoverTip.Row(16, HoverTip.Line(run.When, NativeStyle.CellColor), length),
            HoverTip.Line(run.Outcome, NativeStyle.CellColor with { A = 0.85f }),
            HoverTip.Line(run.Detail, NativeStyle.ColumnHeaderColor, BaselineFontSize));
    }

    private const float PortraitSize = 52f;
    private const float BadgeSize = 34f;
    private const float BadgeOverhang = 8f;
    private const int BadgeFontSize = 18;
    private const float ClockSize = 26f;

    // ── the trend ────────────────────────────────────────────────────────────

    private Control Trend(HomeTrend trend)
    {
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        column.AddThemeConstantOverride("separation", 6);

        column.AddChild(TrendHead(trend));
        column.AddChild(Plot(trend));
        column.AddChild(AxisLabels(trend));

        return NativeStyle.Panel(column, 22, 18, 22, 14);
    }

    /// <summary>
    /// The title, and a legend saying which mark is which. Both series are percentages, so
    /// the legend is the only thing telling the bars and the line apart.
    /// </summary>
    private Control TrendHead(HomeTrend trend)
    {
        var title = NativeStyle.Caption(trend.Title, NativeStyle.CaptionFontSize);
        var underlined = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        underlined.AddThemeConstantOverride("separation", 2);
        underlined.AddChild(title);
        underlined.AddChild(new ColorRect
        {
            Color = NativeStyle.ColumnHeaderColor with { A = 0.4f },
            CustomMinimumSize = new Vector2(0, 1),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        var lines = trend.TipLines;
        var text = HoverTip.TextWidth(TrendTipWidth);
        _tip.Attach(
            underlined,
            () => HoverTip.Column(
                HoverTip.Paragraph(lines[0], NativeStyle.MeasuredColor, text),
                HoverTip.Paragraph(lines[1], NativeStyle.HeaderColor, text),
                HoverTip.Paragraph(lines[2], NativeStyle.ColumnHeaderColor, text, BaselineFontSize)),
            TrendTipWidth);

        var row = HoverTip.Row(
            18,
            underlined,
            HoverTip.Spacer(),
            NativeStyle.Figure("■ block win%", LegendFontSize, NativeStyle.MeasuredColor),
            NativeStyle.Figure("— cumulative", LegendFontSize, NativeStyle.HeaderColor));
        row.MouseFilter = Control.MouseFilterEnum.Pass;
        return row;
    }

    private Control Plot(HomeTrend trend)
    {
        var plot = new Control
        {
            CustomMinimumSize = new Vector2(0, PlotHeight),
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        for (var i = 0; i < GridLines; i++)
        {
            var at = i / (float)(GridLines - 1);
            plot.AddChild(new ColorRect
            {
                // The floor and the ceiling are drawn a shade stronger than the rules
                // between them, so the chart has edges without needing an axis.
                Color = new Color(1f, 1f, 1f, i == 0 || i == GridLines - 1 ? 0.18f : 0.10f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 0f,
                AnchorRight = 1f,
                AnchorTop = at,
                AnchorBottom = at,
                OffsetBottom = 1f,
            });
        }

        var bars = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bars.AddThemeConstantOverride("separation", BarGap);
        bars.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var fills = new List<TextureRect>(trend.Bars.Count);
        foreach (var block in trend.Bars)
        {
            var bar = new Control
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
                CustomMinimumSize = new Vector2(0, Math.Max(BarCapHeight, (float)block.Height * PlotHeight)),
            };

            var fill = NativeStyle.Gradient(BarTop, BarBottom);
            fill.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            bar.AddChild(fill);
            fills.Add(fill);

            // A brighter cap reads as the value's edge, which a flat block does not.
            var cap = new ColorRect
            {
                Color = NativeStyle.MeasuredColor,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 0f,
                AnchorRight = 1f,
                AnchorTop = 0f,
                AnchorBottom = 0f,
                OffsetBottom = BarCapHeight,
            };
            bar.AddChild(cap);
            bars.AddChild(bar);
        }
        plot.AddChild(bars);

        var line = new Line2D
        {
            Width = 4f,
            DefaultColor = NativeStyle.HeaderColor,
            Antialiased = true,
            JointMode = Line2D.LineJointMode.Round,
        };
        plot.AddChild(line);

        // The line's points are pixel positions, and the plot's width is not known until it
        // has been laid out — it takes whatever the content column leaves it. SortChildren
        // fires after the bars have been placed, which is the moment their centres exist.
        bars.Connect(
            Container.SignalName.SortChildren,
            Callable.From(() => Replot(line, bars, trend)));

        plot.AddChild(HoverTargets(trend, fills));
        return WithAxis(plot, trend);
    }

    /// <summary>
    /// Redraw the cumulative line through the centre of each bar. Read off the bars' own
    /// rects rather than recomputed from a width and a gap, so it cannot disagree with where
    /// the container actually put them.
    /// </summary>
    private static void Replot(Line2D line, HBoxContainer bars, HomeTrend trend)
    {
        if (!line.IsValid() || !bars.IsValid())
            return;

        line.ClearPoints();
        for (var i = 0; i < trend.Bars.Count && i < bars.GetChildCount(); i++)
        {
            if (bars.GetChild(i) is not Control bar)
                continue;
            line.AddPoint(new Vector2(
                bar.Position.X + (bar.Size.X / 2f),
                PlotHeight * (1f - (float)trend.Bars[i].Cumulative)));
        }
    }

    /// <summary>
    /// One full-height column per block, so a 4% bar is as easy to hover as a 40% one. The
    /// bar under the cursor lightens, which is what says the tip belongs to it.
    /// </summary>
    private Control HoverTargets(HomeTrend trend, IReadOnlyList<TextureRect> fills)
    {
        var targets = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        targets.AddThemeConstantOverride("separation", BarGap);
        targets.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        for (var i = 0; i < trend.Bars.Count; i++)
        {
            var block = trend.Bars[i];
            var fill = fills[i];
            var target = new Control
            {
                MouseFilter = Control.MouseFilterEnum.Stop,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };

            target.Connect(Control.SignalName.MouseEntered, Callable.From(() =>
            {
                if (fill.IsValid())
                    fill.Modulate = BarHighlight;
            }));
            target.Connect(Control.SignalName.MouseExited, Callable.From(() =>
            {
                if (fill.IsValid())
                    fill.Modulate = Colors.White;
            }));
            _tip.Attach(target, () => BarTip(block), BarTipWidth);
            targets.AddChild(target);
        }
        return targets;
    }

    private static Control BarTip(HomeTrendBar block) =>
        HoverTip.Column(
            HoverTip.Line(block.TipHeading, NativeStyle.CellColor, DetailFontSize, bold: true),
            HoverTip.Row(
                12,
                HoverTip.Line(block.TipRecord, NativeStyle.MeasuredColor),
                HoverTip.Line(block.TipRate, NativeStyle.MeasuredColor, 26, bold: true)),
            HoverTip.Line(block.TipCumulative, NativeStyle.HeaderColor, BaselineFontSize));

    private static readonly Color BarTop = NativeStyle.BarFillColor;
    private static readonly Color BarBottom = new(0.274510f, 0.588235f, 0.705882f, 0.5f);
    private static readonly Color BarHighlight = new(1.2f, 1.2f, 1.2f, 1f);

    /// <summary>
    /// The plot with its axis beside it. Three labels — ceiling, half, nothing — and one
    /// axis for both series, because both of them are percentages.
    /// </summary>
    private static Control WithAxis(Control plot, HomeTrend trend)
    {
        var axis = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(AxisGutter, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Begin,
        };
        axis.AddThemeConstantOverride("separation", 0);

        for (var i = 0; i < trend.AxisLabels.Count; i++)
        {
            var label = NativeStyle.Figure(trend.AxisLabels[i], AxisFontSize, NativeStyle.ColumnHeaderColor);
            label.VerticalAlignment = i == 0
                ? VerticalAlignment.Top
                : i == trend.AxisLabels.Count - 1 ? VerticalAlignment.Bottom : VerticalAlignment.Center;
            label.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            axis.AddChild(label);
        }

        var row = HoverTip.Row(0, plot, axis);
        row.MouseFilter = Control.MouseFilterEnum.Pass;
        return row;
    }

    /// <summary>
    /// The run numbers under the bars. Laid out in the same container shape as the bars —
    /// equal shares, the same gap, the same trailing gutter — so each label sits under the
    /// bar it names without either of them being measured.
    /// </summary>
    private static Control AxisLabels(HomeTrend trend)
    {
        var labels = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        labels.AddThemeConstantOverride("separation", BarGap);

        foreach (var block in trend.Bars)
        {
            var label = NativeStyle.Figure(block.Label, AxisFontSize, NativeStyle.ColumnHeaderColor, bold: false);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            labels.AddChild(label);
        }

        // Wrapped rather than given a trailing child of its own: a spacer inside the row
        // would take the row's separation as well as its width, and every label would sit
        // ten pixels left of the bar it names.
        labels.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return HoverTip.Row(0, labels, new Control
        {
            CustomMinimumSize = new Vector2(AxisGutter, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    // ── row two: the character chips ─────────────────────────────────────────

    private Control CharacterRow(HomePanel panel)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", PanelGap);

        foreach (var character in panel.Characters)
        {
            var chip = Chip(character, Controls.Count);
            chip.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(chip);
            Controls.Add(chip);
        }
        return row;
    }

    /// <summary>
    /// One character: their recent form, and a button that narrows the whole screen to
    /// them. Pressing the selected one lets go again, so the row is a filter you can leave
    /// as easily as you entered it.
    /// </summary>
    private Control Chip(HomeCharacter character, int index)
    {
        var chip = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            FocusMode = Control.FocusModeEnum.All,
        };
        chip.AddThemeStyleboxOverride("panel", ChipBox(character.Selected, lit: false));

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 6);

        var name = NativeStyle.Figure(character.Character, NameFontSize, NativeStyle.CellColor);
        name.VerticalAlignment = VerticalAlignment.Center;
        column.AddChild(HoverTip.Row(
            10, GameArt.IconSlot(ArtKey.Character(character.Character), ChipIconSize), name));

        column.AddChild(HoverTip.Row(
            10,
            NativeStyle.Figure(
                character.LastTenRecord, ChipRecordFontSize, NativeStyle.ToneColor(character.LastTenTone)),
            NativeStyle.Figure("last 10", SmallFontSize + 1, NativeStyle.ColumnHeaderColor, bold: false)));

        column.AddChild(ChipPips(character.RecentRuns));

        column.AddChild(HoverTip.Row(
            8,
            NativeStyle.Figure(character.LastFifty, ChipFiftyFontSize, NativeStyle.CellColor with { A = 0.85f }),
            NativeStyle.Figure("last 50", SmallFontSize, NativeStyle.ColumnHeaderColor, bold: false)));

        var inset = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        inset.AddThemeConstantOverride("margin_left", ChipPaddingX);
        inset.AddThemeConstantOverride("margin_right", ChipPaddingX);
        inset.AddThemeConstantOverride("margin_top", 14);
        inset.AddThemeConstantOverride("margin_bottom", 16);
        inset.AddChild(column);
        chip.AddChild(inset);

        void Light(bool lit) => chip.AddThemeStyleboxOverride("panel", ChipBox(character.Selected, lit));

        chip.Connect(Control.SignalName.MouseEntered, Callable.From(() => Light(true)));
        chip.Connect(Control.SignalName.MouseExited, Callable.From(() => Light(chip.HasFocus())));
        chip.Connect(Control.SignalName.FocusEntered, Callable.From(() => Light(true)));
        chip.Connect(Control.SignalName.FocusExited, Callable.From(() => Light(false)));
        chip.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(input =>
        {
            var pressed = input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
                || input.IsActionPressed(MegaInput.select)
                || input.IsActionPressed(MegaInput.confirm);
            if (!pressed)
                return;
            chip.AcceptEvent();
            _pressed = index;
            _onCharacter(character.Selected ? null : character.Character);
        }));

        return chip;
    }

    /// <summary>
    /// The selected chip wears the same cyan the tab plates and the open combo wear. One
    /// selection colour on the screen, so what it means never has to be worked out twice.
    /// </summary>
    private static StyleBoxFlat ChipBox(bool selected, bool lit) => new()
    {
        BgColor = selected ? NativeStyle.FocusColor with { A = 0.12f } : NativeStyle.PanelColor,
        BorderColor = selected || lit ? NativeStyle.FocusColor : NativeStyle.CellColor with { A = 0.08f },
        BorderWidthLeft = 2,
        BorderWidthTop = 2,
        BorderWidthRight = 2,
        BorderWidthBottom = 2,
        ShadowColor = selected || lit ? NativeStyle.FocusColor with { A = 0.28f } : new Color(0, 0, 0, 0),
        ShadowSize = selected || lit ? 14 : 0,
    };

    private static Control ChipPips(IReadOnlyList<bool> runs)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", ChipPipGap);

        foreach (var win in runs)
            row.AddChild(new ColorRect
            {
                Color = win ? NativeStyle.GoodColor : NativeStyle.CellColor with { A = 0.18f },
                CustomMinimumSize = new Vector2(0, ChipPipHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        return row;
    }

    // ── row three: the four boxes ────────────────────────────────────────────

    private static Control StatRow(HomePanel panel)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", PanelGap);

        foreach (var stat in panel.Stats)
        {
            var box = StatBox(stat);
            box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(box);
        }
        return row;
    }

    private static Control StatBox(HomeStat stat)
    {
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(NativeStyle.Caption(stat.Caption));

        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 10);
        line.AddChild(NativeStyle.Figure(stat.Value, NativeStyle.StatFontSize, NativeStyle.ToneColor(stat.ValueTone)));
        if (stat.Detail.Length > 0)
            line.AddChild(NativeStyle.Figure(
                stat.Detail, DetailFontSize, NativeStyle.CellColor with { A = 0.55f }, bold: false));
        line.AddChild(HoverTip.Spacer());
        if (stat.Delta.Length > 0)
            line.AddChild(NativeStyle.Figure(stat.Delta, DetailFontSize, NativeStyle.ToneColor(stat.DeltaTone)));
        column.AddChild(line);

        return NativeStyle.Panel(column, 16, 12, 16, 14);
    }
}

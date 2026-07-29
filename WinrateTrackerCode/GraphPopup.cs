using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// A period table, plotted: one bar per period showing the wins in it, with the all-time
/// win rate drawn over the top as a line.
///
/// Two quantities on two scales, so both axes are labelled — wins on the left, win rate on
/// the right — and each is drawn in the colour of the thing it measures. Without that, a
/// tall bar and a high line look like the same claim when they are not: a good month and a
/// busy month both make a tall bar.
///
/// Drawn from primitives rather than a chart widget: the game has none, and a mod cannot
/// attach a script to run <c>_Draw</c>. Bars and rules are <see cref="ColorRect" />, the
/// average is a <see cref="Line2D" />, and both are engine types a mod can construct.
/// </summary>
internal sealed class GraphPopup
{
    private const float PanelWidth = 1480f;
    private const float PanelHeight = 760f;
    private const float PlotInsetLeft = 128f;
    private const float PlotInsetRight = 128f;
    private const float PlotInsetTop = 152f;
    private const float PlotInsetBottom = 152f;

    /// <summary>Share of each slot the bar fills, leaving the rest as breathing room.</summary>
    private const float BarFill = 0.62f;

    private const float MinBarWidth = 3f;
    private const float LineWidth = 3f;
    private const float MarkerSize = 9f;
    private const float AxisWidth = 2f;
    private const int GridDivisions = 4;

    /// <summary>At most this many x-axis labels, so they cannot collide.</summary>
    private const int MaxAxisLabels = 12;

    private static readonly Color BarColor = new(0.28f, 0.62f, 0.78f, 0.9f);
    private static readonly Color BarTopColor = new(0.55f, 0.85f, 0.97f, 1f);
    private static readonly Color BarHoverColor = new(0.55f, 0.85f, 0.97f, 0.55f);
    private static readonly Color LineColor = new(0.937255f, 0.784314f, 0.317647f, 1f);
    private static readonly Color GridColor = new(1f, 1f, 1f, 0.10f);
    private static readonly Color AxisColor = new(1f, 1f, 1f, 0.28f);

    private readonly Control _root;
    private Tooltip? _tooltip;

    private GraphPopup(Control root) => _root = root;

    /// <summary>
    /// Show <paramref name="section" />'s series over <paramref name="host" />, replacing
    /// any graph already up. Returns the popup so the caller can close it.
    /// </summary>
    public static GraphPopup Show(Control host, TableSection section)
    {
        var backdrop = new ColorRect
        {
            Name = "WinrateGraphPopup",
            Color = StsColors.screenBackdrop,
            // Stop, so the table underneath cannot be clicked through the graph.
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(backdrop);

        var popup = new GraphPopup(backdrop);
        // Clicking away is the dismissal people try first, so it works before they find
        // the button.
        backdrop.Connect(
            Control.SignalName.GuiInput,
            Callable.From<InputEvent>(input =>
            {
                if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                    popup.Close();
            }));

        backdrop.AddChild(popup.BuildPanel(section));
        return popup;
    }

    public void Close()
    {
        if (GodotObject.IsInstanceValid(_root))
            _root.QueueFree();
    }

    private Control BuildPanel(TableSection section)
    {
        var panel = new Control
        {
            CustomMinimumSize = new Vector2(PanelWidth, PanelHeight),
            // Stop, so a click on the panel does not reach the backdrop and close it.
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.OffsetLeft = -PanelWidth / 2f;
        panel.OffsetRight = PanelWidth / 2f;
        panel.OffsetTop = -PanelHeight / 2f;
        panel.OffsetBottom = PanelHeight / 2f;

        var background = new ColorRect
        {
            Color = NativeStyle.PanelColor with { A = 0.98f },
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(background);

        var title = NativeStyle.Header(section.Title);
        title.Position = new Vector2(PlotInsetLeft, 26f);
        panel.AddChild(title);

        panel.AddChild(Caption("wins per period", BarTopColor, new Vector2(PlotInsetLeft, 88f)));
        panel.AddChild(Caption("win rate overall", LineColor, new Vector2(PlotInsetLeft + 260f, 88f)));

        var close = NativeStyle.TextButton("Close", Close);
        close.Position = new Vector2(PanelWidth - PlotInsetRight - 224f, 22f);
        panel.AddChild(close);

        panel.AddChild(BuildPlot(section.Series!));

        _tooltip = new Tooltip(panel);
        return panel;
    }

    private static MegaLabel Caption(string text, Color color, Vector2 position)
    {
        var label = NativeStyle.Cell(text, rightAligned: false, header: true);
        label.AddThemeColorOverride("font_color", color);
        label.Position = position;
        return label;
    }

    private Control BuildPlot(IReadOnlyList<SeriesPoint> series)
    {
        var plot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        plot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        plot.OffsetLeft = PlotInsetLeft;
        plot.OffsetRight = -PlotInsetRight;
        plot.OffsetTop = PlotInsetTop;
        plot.OffsetBottom = -PlotInsetBottom;

        var width = PanelWidth - PlotInsetLeft - PlotInsetRight;
        var height = PanelHeight - PlotInsetTop - PlotInsetBottom;

        // Bars are scaled against a rounded ceiling rather than the tallest bar, so the
        // left axis can carry whole numbers and the best period is not always drawn
        // touching the top of the plot.
        var ceiling = NiceCeiling(series.Max(point => point.Wins));
        var slot = width / series.Count;
        var barWidth = Math.Max(MinBarWidth, slot * BarFill);

        AddGrid(plot, width, height, ceiling);
        AddAxes(plot, width, height);

        var bars = new List<ColorRect>(series.Count);
        for (var i = 0; i < series.Count; i++)
        {
            var barHeight = height * ((float)series[i].Wins / ceiling);
            var left = i * slot + (slot - barWidth) / 2f;

            var bar = new ColorRect
            {
                Color = BarColor,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Position = new Vector2(left, height - barHeight),
                Size = new Vector2(barWidth, barHeight),
            };
            plot.AddChild(bar);
            bars.Add(bar);

            // A brighter cap reads as the value's edge, which a flat block does not.
            if (barHeight >= 2f)
                plot.AddChild(new ColorRect
                {
                    Color = BarTopColor,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Position = new Vector2(left, height - barHeight),
                    Size = new Vector2(barWidth, 2f),
                });
        }

        var line = new Line2D { Width = LineWidth, DefaultColor = LineColor, Antialiased = true };
        for (var i = 0; i < series.Count; i++)
            line.AddPoint(PointAt(series, i, slot, height));
        plot.AddChild(line);

        // A marker per period says where a reading actually is, rather than leaving the
        // eye to guess along a smooth line.
        for (var i = 0; i < series.Count; i++)
        {
            var at = PointAt(series, i, slot, height);
            plot.AddChild(new ColorRect
            {
                Color = LineColor,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Position = at - new Vector2(MarkerSize / 2f, MarkerSize / 2f),
                Size = new Vector2(MarkerSize, MarkerSize),
            });
        }

        AddAxisLabels(plot, series, slot, height);
        AddHoverTargets(plot, series, slot, height, bars);
        return plot;
    }

    private static Vector2 PointAt(IReadOnlyList<SeriesPoint> series, int index, float slot, float height) =>
        new(index * slot + slot / 2f, height * (1f - (float)series[index].CumulativeWinRate));

    /// <summary>
    /// Horizontal rules with a scale on each side: wins on the left in the bars' colour,
    /// win rate on the right in the line's.
    /// </summary>
    private static void AddGrid(Control plot, float width, float height, int ceiling)
    {
        for (var mark = 0; mark <= GridDivisions; mark++)
        {
            var fraction = mark / (float)GridDivisions;
            var y = height * (1f - fraction);

            if (mark > 0)
                plot.AddChild(new ColorRect
                {
                    Color = GridColor,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Position = new Vector2(0, y),
                    Size = new Vector2(width, 1),
                });

            var wins = NativeStyle.Cell(
                Format.Count((int)Math.Round(ceiling * fraction)),
                rightAligned: true,
                header: true);
            wins.AddThemeColorOverride("font_color", BarTopColor);
            wins.Position = new Vector2(-96f, y - 14f);
            wins.CustomMinimumSize = new Vector2(84f, 0);
            wins.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            plot.AddChild(wins);

            var rate = NativeStyle.Cell(Format.WholePercent(fraction), rightAligned: false, header: true);
            rate.AddThemeColorOverride("font_color", LineColor);
            rate.Position = new Vector2(width + 14f, y - 14f);
            plot.AddChild(rate);
        }
    }

    private static void AddAxes(Control plot, float width, float height)
    {
        plot.AddChild(new ColorRect
        {
            Color = AxisColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(0, height),
            Size = new Vector2(width, AxisWidth),
        });
        plot.AddChild(new ColorRect
        {
            Color = AxisColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = Vector2.Zero,
            Size = new Vector2(AxisWidth, height),
        });
        plot.AddChild(new ColorRect
        {
            Color = AxisColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(width - AxisWidth, 0),
            Size = new Vector2(AxisWidth, height),
        });
    }

    /// <summary>
    /// One invisible column per period, spanning the plot's full height. Both the bar and
    /// the line's marker for a period live in that column, so a single target serves both
    /// and there is no narrow point to chase with the mouse.
    /// </summary>
    private void AddHoverTargets(
        Control plot,
        IReadOnlyList<SeriesPoint> series,
        float slot,
        float height,
        IReadOnlyList<ColorRect> bars)
    {
        for (var i = 0; i < series.Count; i++)
        {
            var point = series[i];
            var bar = bars[i];
            var highlight = new ColorRect
            {
                Color = new Color(1, 1, 1, 0),
                MouseFilter = Control.MouseFilterEnum.Stop,
                Position = new Vector2(i * slot, 0),
                Size = new Vector2(slot, height),
            };
            plot.AddChild(highlight);

            highlight.Connect(Control.SignalName.MouseEntered, Callable.From(() =>
            {
                highlight.Color = new Color(1, 1, 1, 0.06f);
                if (GodotObject.IsInstanceValid(bar))
                    bar.Color = BarHoverColor;
                _tooltip?.Show(point);
            }));
            highlight.Connect(Control.SignalName.MouseExited, Callable.From(() =>
            {
                highlight.Color = new Color(1, 1, 1, 0);
                if (GodotObject.IsInstanceValid(bar))
                    bar.Color = BarColor;
                _tooltip?.Hide();
            }));
        }
    }

    /// <summary>
    /// First and last period always get a label, with a few evenly spaced in between —
    /// enough to place the line in time without the axis turning into a smear.
    /// </summary>
    private static void AddAxisLabels(Control plot, IReadOnlyList<SeriesPoint> series, float slot, float height)
    {
        var step = Math.Max(1, (int)Math.Ceiling(series.Count / (double)MaxAxisLabels));
        for (var i = 0; i < series.Count; i++)
        {
            if (i % step != 0 && i != series.Count - 1)
                continue;
            var label = NativeStyle.Cell(series[i].Label, rightAligned: false, header: true);
            label.Position = new Vector2(i * slot, height + 14f);
            plot.AddChild(label);
        }
    }

    /// <summary>
    /// Round a maximum up to something a person would put on an axis: 1, 2, or 5 times a
    /// power of ten. A peak of 34 becomes 40, not 34, so the gridlines land on 10s.
    /// </summary>
    private static int NiceCeiling(int peak)
    {
        if (peak <= GridDivisions)
            return Math.Max(GridDivisions, 1);

        var magnitude = (int)Math.Pow(10, Math.Floor(Math.Log10(peak)));
        foreach (var step in new[] { 1, 2, 5, 10 })
        {
            var candidate = step * magnitude;
            // Divisible by the grid so every rule gets a whole number.
            if (candidate >= peak && candidate % GridDivisions == 0)
                return candidate;
        }
        return (int)Math.Ceiling(peak / (double)GridDivisions) * GridDivisions;
    }

    /// <summary>
    /// The readout for the period under the cursor. Its own panel rather than the game's
    /// hover tip system, which is wired to the tip stack and to card holders and has no
    /// business being dragged into a modal graph.
    /// </summary>
    private sealed class Tooltip
    {
        private const float Width = 300f;
        private const float Height = 132f;
        private const float Margin = 20f;

        private readonly Control _panel;
        private readonly MegaLabel _label;

        public Tooltip(Control host)
        {
            _panel = new Control
            {
                CustomMinimumSize = new Vector2(Width, Height),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false,
                ZIndex = 10,
            };
            _panel.Size = new Vector2(Width, Height);

            var background = new ColorRect
            {
                Color = new Color(0.04f, 0.06f, 0.07f, 0.96f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _panel.AddChild(background);

            _label = NativeStyle.Cell("", rightAligned: false);
            _label.Position = new Vector2(16f, 12f);
            _panel.AddChild(_label);

            host.AddChild(_panel);
        }

        public void Show(SeriesPoint point)
        {
            if (!GodotObject.IsInstanceValid(_panel))
                return;

            var tally = new Tally(point.Runs, point.Wins);
            _label.Text = string.Join(
                '\n',
                point.Label,
                $"{point.Runs} runs · {Format.WinLoss(tally)}",
                $"win rate {Format.WholePercent(tally)}",
                $"overall {Format.Percent(point.CumulativeWinRate)}");

            // Follows the cursor, clamped so it never hangs off the panel.
            var mouse = _panel.GetParent<Control>().GetLocalMousePosition();
            var bounds = _panel.GetParent<Control>().Size;
            _panel.Position = new Vector2(
                Math.Clamp(mouse.X + Margin, 0, Math.Max(0, bounds.X - Width)),
                Math.Clamp(mouse.Y + Margin, 0, Math.Max(0, bounds.Y - Height)));
            _panel.Visible = true;
        }

        public void Hide()
        {
            if (GodotObject.IsInstanceValid(_panel))
                _panel.Visible = false;
        }
    }
}

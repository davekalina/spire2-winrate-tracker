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
    private const float PlotInsetLeft = ModalPanel.ContentInsetLeft;
    private const float PlotInsetRight = ModalPanel.ContentInsetRight;
    private const float PlotInsetTop = ModalPanel.ContentInsetTop;
    private const float PlotInsetBottom = ModalPanel.ContentInsetBottom;

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

    private readonly ModalPanel _modal;
    private Tooltip? _tooltip;

    private GraphPopup(ModalPanel modal) => _modal = modal;

    /// <summary>
    /// Show <paramref name="section" />'s series over <paramref name="host" />, replacing
    /// any graph already up. Returns the popup so the caller can close it.
    /// </summary>
    public static GraphPopup Show(Control host, TableSection section)
    {
        var modal = ModalPanel.Open(host, section.Title, PanelWidth, PanelHeight);
        var popup = new GraphPopup(modal);

        modal.Panel.AddChild(Caption("wins per period", BarTopColor, new Vector2(PlotInsetLeft, 88f)));
        modal.Panel.AddChild(Caption("win rate cumulative", LineColor, new Vector2(PlotInsetLeft + 280f, 88f)));

        popup.BuildPlot(modal.Content, section.Series!);
        popup._tooltip = new Tooltip(modal.Panel);
        return popup;
    }

    public void Close() => _modal.Close();

    private static MegaLabel Caption(string text, Color color, Vector2 position)
    {
        var label = NativeStyle.Cell(text, rightAligned: false, header: true);
        label.AddThemeColorOverride("font_color", color);
        label.Position = position;
        return label;
    }

    private void BuildPlot(Control plot, IReadOnlyList<SeriesPoint> series)
    {
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
        private const float PaddingX = 18f;
        private const float PaddingY = 12f;
        private const float LineHeight = NativeStyle.CellFontSize + 8f;
        private const float Margin = 20f;

        private readonly Control _panel;
        private readonly MegaLabel _label;

        public Tooltip(Control host)
        {
            _panel = new Control
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false,
                ZIndex = 10,
            };

            var background = new ColorRect
            {
                Color = new Color(0.04f, 0.06f, 0.07f, 0.96f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _panel.AddChild(background);

            _label = NativeStyle.Cell("", rightAligned: false);
            _label.Position = new Vector2(PaddingX, PaddingY);
            _panel.AddChild(_label);

            host.AddChild(_panel);
        }

        public void Show(SeriesPoint point)
        {
            if (!GodotObject.IsInstanceValid(_panel))
                return;

            var tally = new Tally(point.Runs, point.Wins);
            string[] lines =
            [
                point.Label,
                $"{point.Runs} runs · {Format.WinLoss(tally)}",
                $"win rate {Format.WholePercent(tally)}",
                $"cumulative {Format.Percent(point.CumulativeWinRate)}",
            ];
            _label.Text = string.Join('\n', lines);

            // Sized to the text it is actually showing. A fixed box either clips the
            // longest reading or leaves a slab of empty panel beside the shortest.
            var size = new Vector2(
                lines.Max(NativeStyle.MeasureCell) + PaddingX * 2f,
                lines.Length * LineHeight + PaddingY * 2f);
            _panel.CustomMinimumSize = size;
            _panel.Size = size;

            // Follows the cursor, clamped so it never hangs off the panel.
            var host = _panel.GetParent<Control>();
            var mouse = host.GetLocalMousePosition();
            var bounds = host.Size;
            _panel.Position = new Vector2(
                Math.Clamp(mouse.X + Margin, 0, Math.Max(0, bounds.X - size.X)),
                Math.Clamp(mouse.Y + Margin, 0, Math.Max(0, bounds.Y - size.Y)));
            _panel.Visible = true;
        }

        public void Hide()
        {
            if (GodotObject.IsInstanceValid(_panel))
                _panel.Visible = false;
        }
    }
}

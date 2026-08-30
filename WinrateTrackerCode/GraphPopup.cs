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

    // The chart colours are the screen's, not this popup's. The Home trend draws the same
    // two series, and two definitions of "the bar colour" is how they come to disagree.
    private static readonly Color BarColor = new(0.28f, 0.62f, 0.78f, 0.9f);
    private static readonly Color BarTopColor = NativeStyle.MeasuredColor;
    private static readonly Color BarHoverColor = NativeStyle.MeasuredColor with { A = 0.55f };
    private static readonly Color LineColor = NativeStyle.HeaderColor;
    private static readonly Color GridColor = new(1f, 1f, 1f, 0.10f);
    private static readonly Color AxisColor = new(1f, 1f, 1f, 0.28f);

    private readonly ModalPanel _modal;
    private HoverTip? _tooltip;

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

        // The screen's own tip, on the modal rather than under it. Sharing it is why the
        // readout here and the one on the Home trend cannot drift into two widgets.
        //
        // Before the plot, not after: the plot attaches its hover targets to this as it
        // builds them, and a tip that did not exist yet would leave every bar silent.
        popup._tooltip = new HoverTip(modal.Panel);
        popup.BuildPlot(modal.Content, section.Series!);
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
            }));
            highlight.Connect(Control.SignalName.MouseExited, Callable.From(() =>
            {
                highlight.Color = new Color(1, 1, 1, 0);
                if (GodotObject.IsInstanceValid(bar))
                    bar.Color = BarColor;
            }));
            _tooltip?.Attach(highlight, () => PeriodTip(point), TooltipWidth);
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
    /// The readout for the period the cursor is on: what the bar is worth on its own, and
    /// where the line had got to by the end of it. Two quantities on two scales, so each is
    /// named and drawn in the colour of the mark that carries it.
    /// </summary>
    private static Control PeriodTip(SeriesPoint point)
    {
        var tally = new Tally(point.Runs, point.Wins);
        return HoverTip.Column(
            HoverTip.Line(point.Label, NativeStyle.CellColor, bold: true),
            HoverTip.Row(
                12,
                HoverTip.Line($"{point.Runs} runs", NativeStyle.ColumnHeaderColor),
                HoverTip.Line(Format.WinLoss(tally), BarTopColor),
                HoverTip.Line(Format.WholePercent(tally), BarTopColor, bold: true)),
            HoverTip.Line($"cumulative {Format.Percent(point.CumulativeWinRate)}", LineColor));
    }

    private const float TooltipWidth = 340f;
}

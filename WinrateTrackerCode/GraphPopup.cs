using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// A period table, plotted: one bar per period showing wins, with the all-time win rate
/// drawn over the top as a line.
///
/// The two are on different scales on purpose. The bars answer "how much did I play and
/// how much did I win" and the line answers "where is the average heading" — laying the
/// second over the first is the whole point, since a tall bar in a short period and a
/// short bar in a long one are the same news.
///
/// Drawn from primitives rather than a chart widget: the game has none, and a mod cannot
/// attach a script to run <c>_Draw</c>. Bars are <see cref="ColorRect" />, the average is
/// a <see cref="Line2D" />, and both are engine types a mod can construct.
/// </summary>
internal sealed class GraphPopup
{
    private const float PanelWidth = 1400f;
    private const float PanelHeight = 720f;
    private const float PlotInsetLeft = 96f;
    private const float PlotInsetRight = 96f;
    private const float PlotInsetTop = 128f;
    private const float PlotInsetBottom = 132f;
    private const float BarGap = 4f;
    private const float MinBarWidth = 3f;
    private const float LineWidth = 3f;

    /// <summary>At most this many x-axis labels, so they cannot collide.</summary>
    private const int MaxAxisLabels = 12;

    private static readonly Color BarColor = new(0.35f, 0.72f, 0.85f, 0.95f);
    private static readonly Color LineColor = new(0.937255f, 0.784314f, 0.317647f, 1f);
    private static readonly Color GridColor = new(1f, 1f, 1f, 0.12f);

    private readonly Control _root;

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

        backdrop.AddChild(BuildPanel(section, popup));
        return popup;
    }

    public void Close()
    {
        if (GodotObject.IsInstanceValid(_root))
            _root.QueueFree();
    }

    private static Control BuildPanel(TableSection section, GraphPopup popup)
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
            Color = NativeStyle.PanelColor with { A = 0.97f },
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(background);

        var title = NativeStyle.Header(section.Title);
        title.Position = new Vector2(PlotInsetLeft, 28f);
        panel.AddChild(title);

        var legend = NativeStyle.Cell("bars: wins per period · line: win rate overall", rightAligned: false, header: true);
        legend.Position = new Vector2(PlotInsetLeft, 84f);
        panel.AddChild(legend);

        var close = NativeStyle.TextButton("Close", popup.Close);
        close.Position = new Vector2(PanelWidth - PlotInsetRight - 180f, 24f);
        panel.AddChild(close);

        panel.AddChild(BuildPlot(section.Series!));
        return panel;
    }

    private static Control BuildPlot(IReadOnlyList<SeriesPoint> series)
    {
        var plot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        plot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        plot.OffsetLeft = PlotInsetLeft;
        plot.OffsetRight = -PlotInsetRight;
        plot.OffsetTop = PlotInsetTop;
        plot.OffsetBottom = -PlotInsetBottom;

        var width = PanelWidth - PlotInsetLeft - PlotInsetRight;
        var height = PanelHeight - PlotInsetTop - PlotInsetBottom;

        // Bars are scaled against the busiest period, so the tallest bar always fills the
        // plot and short archives are not drawn as a flat line along the bottom.
        var peakWins = Math.Max(1, series.Max(point => point.Wins));
        var slot = width / series.Count;
        var barWidth = Math.Max(MinBarWidth, slot - BarGap);

        // The rate line runs 0-100%, so a horizontal rule every 25% says which is which.
        for (var mark = 1; mark <= 3; mark++)
        {
            var rule = new ColorRect
            {
                Color = GridColor,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Position = new Vector2(0, height * (1f - mark * 0.25f)),
                Size = new Vector2(width, 1),
            };
            plot.AddChild(rule);
            var label = NativeStyle.Cell($"{mark * 25}%", rightAligned: false, header: true);
            label.Position = new Vector2(width + 8f, height * (1f - mark * 0.25f) - 14f);
            plot.AddChild(label);
        }

        for (var i = 0; i < series.Count; i++)
        {
            var barHeight = height * ((float)series[i].Wins / peakWins);
            plot.AddChild(new ColorRect
            {
                Color = BarColor,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Position = new Vector2(i * slot + BarGap / 2f, height - barHeight),
                Size = new Vector2(barWidth, barHeight),
            });
        }

        var line = new Line2D
        {
            Width = LineWidth,
            DefaultColor = LineColor,
            Antialiased = true,
        };
        for (var i = 0; i < series.Count; i++)
            line.AddPoint(new Vector2(
                i * slot + slot / 2f,
                height * (1f - (float)series[i].CumulativeWinRate)));
        plot.AddChild(line);

        AddAxisLabels(plot, series, slot, height);
        return plot;
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
            label.Position = new Vector2(i * slot, height + 12f);
            plot.AddChild(label);
        }
    }
}

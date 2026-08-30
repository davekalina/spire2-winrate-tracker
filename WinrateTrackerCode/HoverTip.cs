using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The screen's hover tips: a framed panel that appears under whatever the cursor is on.
///
/// One of these per screen, reused by everything that has something to explain — a run
/// pip, a trend bar, the trend's own title, a column heading. Sharing it is what keeps
/// them one widget rather than four that drifted apart, and it means only one tip can ever
/// be up at a time, which is the behaviour anyway.
///
/// It is the mod's own panel rather than the game's hover-tip system. That system is wired
/// to the tip stack and to card holders, and has no business being dragged into a table of
/// numbers; but it wears the same skin as the drop-down lists here, so it still reads as
/// part of the game's furniture. See <see cref="NativeStyle.PopupBox" />.
///
/// Tips open <em>below</em> what they describe, always. On a screen where the interesting
/// thing is usually the panel above the cursor, a tip that opened upwards would cover the
/// figure it was sent to explain.
/// </summary>
internal sealed class HoverTip
{
    /// <summary>Clearance between the thing described and the tip describing it.</summary>
    private const float Gap = 10f;

    /// <summary>Inset inside the frame, from the design's 14/18/16.</summary>
    private const int PaddingX = 18;

    private const int PaddingTop = 14;
    private const int PaddingBottom = 16;

    /// <summary>Keeps the tip clear of the screen edge when it would otherwise run off.</summary>
    private const float Margin = 12f;

    private readonly Control _host;
    private readonly PanelContainer _panel;
    private readonly MarginContainer _inset;
    private Control? _content;

    public HoverTip(Control host)
    {
        _host = host;

        _panel = new PanelContainer
        {
            Name = "WinrateHoverTip",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            // Over the tables, the filter band, and anything else on the screen. A tip that
            // opens behind the panel below it is worse than no tip.
            ZIndex = 60,
        };
        _panel.AddThemeStyleboxOverride("panel", NativeStyle.PopupBox());

        _inset = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _inset.AddThemeConstantOverride("margin_left", PaddingX);
        _inset.AddThemeConstantOverride("margin_right", PaddingX);
        _inset.AddThemeConstantOverride("margin_top", PaddingTop);
        _inset.AddThemeConstantOverride("margin_bottom", PaddingBottom);
        _panel.AddChild(_inset);

        host.AddChild(_panel);
    }

    /// <summary>
    /// Show <paramref name="content" /> under <paramref name="anchor" />.
    ///
    /// <paramref name="width" /> is the tip's width, which the caller sets rather than the
    /// content: these tips read as a set, and letting each size itself to its longest line
    /// makes four differently-shaped panels out of one widget.
    /// </summary>
    public void Show(Control anchor, Control content, float width)
    {
        if (!GodotObject.IsInstanceValid(_panel) || !anchor.IsInsideTree() || !_host.IsInsideTree())
            return;

        Clear();
        content.MouseFilter = Control.MouseFilterEnum.Ignore;
        content.CustomMinimumSize = new Vector2(width - (PaddingX * 2), 0);
        _inset.AddChild(content);
        _content = content;

        _panel.Visible = true;
        _panel.Size = new Vector2(width, _panel.GetCombinedMinimumSize().Y);
        Place(anchor);
    }

    /// <summary>
    /// Put the tip under the anchor and keep it on screen.
    ///
    /// Both rects are read in global space and differenced, because the anchor is usually
    /// buried several containers deep inside the scrolling body while the tip is parented to
    /// the screen — there is no shared parent whose coordinates both are already in.
    /// </summary>
    private void Place(Control anchor)
    {
        var anchorRect = anchor.GetGlobalRect();
        var hostRect = _host.GetGlobalRect();
        var size = _panel.Size;

        var left = anchorRect.Position.X - hostRect.Position.X;
        var top = anchorRect.Position.Y - hostRect.Position.Y + anchorRect.Size.Y + Gap;

        _panel.Position = new Vector2(
            Math.Clamp(left, Margin, Math.Max(Margin, hostRect.Size.X - size.X - Margin)),
            Math.Clamp(top, Margin, Math.Max(Margin, hostRect.Size.Y - size.Y - Margin)));
    }

    public void Hide()
    {
        if (!GodotObject.IsInstanceValid(_panel))
            return;
        _panel.Visible = false;
        Clear();
    }

    private void Clear()
    {
        if (_content is null)
            return;
        if (GodotObject.IsInstanceValid(_content))
        {
            _inset.RemoveChild(_content);
            _content.QueueFree();
        }
        _content = null;
    }

    /// <summary>
    /// Make <paramref name="target" /> show a tip while the cursor is over it.
    ///
    /// The content is built on each hover rather than once, so a tip always reads the
    /// figures that are on screen now — the tables are rebuilt on every filter change, and
    /// a tip built at construction would outlive the row it described.
    /// </summary>
    public void Attach(Control target, Func<Control> content, float width)
    {
        target.MouseFilter = Control.MouseFilterEnum.Stop;
        target.Connect(Control.SignalName.MouseEntered, Callable.From(() => Show(target, content(), width)));
        target.Connect(Control.SignalName.MouseExited, Callable.From(Hide));
        // A tip whose row is freed underneath it — which happens on every rebuild — would
        // otherwise be left on screen with nothing under the cursor to dismiss it.
        target.Connect(Node.SignalName.TreeExiting, Callable.From(Hide));
    }

    // ── content ──────────────────────────────────────────────────────────────

    /// <summary>A column of rows, spaced the way every tip on this screen spaces them.</summary>
    public static VBoxContainer Column(params Control[] rows)
    {
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 8);
        foreach (var row in rows)
            column.AddChild(row);
        return column;
    }

    public static MegaLabel Line(string text, Color color, int size = LineFontSize, bool bold = false)
    {
        var label = NativeStyle.Figure(text, size, color, bold);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.VerticalAlignment = VerticalAlignment.Top;
        return label;
    }

    private const int LineFontSize = 22;

    /// <summary>A row of controls laid out left to right, with the last one pushed right.</summary>
    public static HBoxContainer Row(int separation, params Control[] parts)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", separation);
        foreach (var part in parts)
            row.AddChild(part);
        return row;
    }

    /// <summary>Pushes whatever follows it to the right-hand edge of its row.</summary>
    public static Control Spacer() =>
        new()
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
}

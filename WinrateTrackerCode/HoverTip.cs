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
    /// <param name="framed">
    /// False for content that is already a finished picture — a card, which the game draws
    /// with its own border and its own transparent margins. A frame round that reads as a
    /// second border nobody asked for.
    /// </param>
    public void Show(Control anchor, Control content, float width, bool framed = true)
    {
        if (!GodotObject.IsInstanceValid(_panel) || !anchor.IsInsideTree() || !_host.IsInsideTree())
            return;

        Clear();
        content.MouseFilter = Control.MouseFilterEnum.Ignore;
        // A width of nothing means the content knows its own size: a card scene does, a run
        // of prose does not and has to be told before it can say how tall it is.
        if (width > 0f)
            content.CustomMinimumSize = new Vector2(TextWidth(width), 0);

        _panel.AddThemeStyleboxOverride("panel", framed ? NativeStyle.PopupBox() : new StyleBoxEmpty());
        var inset = framed ? PaddingX : 0;
        _inset.AddThemeConstantOverride("margin_left", inset);
        _inset.AddThemeConstantOverride("margin_right", inset);
        _inset.AddThemeConstantOverride("margin_top", framed ? PaddingTop : 0);
        _inset.AddThemeConstantOverride("margin_bottom", framed ? PaddingBottom : 0);

        _inset.AddChild(content);
        _content = content;

        _panel.Visible = true;
        var minimum = _panel.GetCombinedMinimumSize();
        _panel.Size = new Vector2(width > 0f ? width : minimum.X, minimum.Y);
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
    public void Attach(Control target, Func<Control> content, float width, bool framed = true)
    {
        target.MouseFilter = Control.MouseFilterEnum.Stop;
        target.Connect(
            Control.SignalName.MouseEntered,
            Callable.From(() => Show(target, content(), width, framed)));
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

    /// <summary>
    /// One line of a tip. Deliberately not autowrapped — see <see cref="Paragraph" />.
    /// </summary>
    public static MegaLabel Line(string text, Color color, int size = LineFontSize, bool bold = false)
    {
        var label = NativeStyle.Figure(text, size, color, bold);
        label.VerticalAlignment = VerticalAlignment.Top;
        return label;
    }

    private const int LineFontSize = 22;

    /// <summary>The room a tip of this width leaves for text, inside its frame.</summary>
    public static float TextWidth(float width) => width - (PaddingX * 2);

    /// <summary>
    /// A run of prose, broken to fit.
    ///
    /// The wrapping is done here, by measuring, rather than by Godot's
    /// <c>AutowrapMode</c>. An autowrapping label reports a minimum width of nothing — it
    /// will always wrap harder — and a minimum height computed from the width it currently
    /// has. Inside a panel being sized in the same frame that width is zero, so the label
    /// asks for one character per line and a column hundreds of pixels tall, and the tip is
    /// sized to that. Measuring gives an exact minimum in both directions and the panel
    /// comes out the shape it is supposed to be.
    /// </summary>
    public static MegaLabel Paragraph(string text, Color color, float width, int size = LineFontSize) =>
        Line(Wrap(text, width, size), color, size);

    /// <summary>
    /// Greedy line-breaking on whole words, measured in the font it will be drawn in. A
    /// word too long for the line is left to overhang rather than broken: these are English
    /// sentences and card names, and a hyphen in the middle of one reads as a bug.
    /// </summary>
    private static string Wrap(string text, float width, int size)
    {
        var lines = new List<string>();
        var line = new System.Text.StringBuilder();

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && NativeStyle.Measure(candidate, size, bold: false) > width)
            {
                lines.Add(line.ToString());
                line.Clear();
                line.Append(word);
            }
            else
            {
                line.Clear();
                line.Append(candidate);
            }
        }

        if (line.Length > 0)
            lines.Add(line.ToString());
        return string.Join('\n', lines);
    }

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

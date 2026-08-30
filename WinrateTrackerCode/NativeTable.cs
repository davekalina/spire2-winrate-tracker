using Godot;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Renders a <see cref="TableSection" /> as native controls.
///
/// Each row is its own container on its own background, rather than every cell going into
/// one <see cref="GridContainer" />. The grid was simpler and could not be kept: a grid
/// cannot paint a row, and the alternating stripe is what holds the eye on one line across
/// eight columns. So the columns are measured once for the whole table — the widest
/// heading or cell in each — and every row is laid out to those same widths, which gets
/// back the alignment the grid was giving for free.
///
/// The first column takes the slack and everything else keeps its measured width, so the
/// numbers pack against the right-hand side of the panel while the labels stay put. That
/// is the thing that makes a dense table scannable, and it is the same arrangement the
/// grid produced with its expand flags.
/// </summary>
internal static class NativeTable
{
    /// <summary>A character icon, in a row already made tall by its pips.</summary>
    private const float TallRowIconSize = 38f;

    /// <summary>A rarity icon, which has to fit a row of plain text.</summary>
    private const float TextRowIconSize = 28f;

    /// <summary>The same in a column heading, where it sits beside smaller text.</summary>
    private const float HeaderIconSize = 34f;

    private const int IconGap = 12;

    /// <summary>A pip in a table row. Smaller than Home's, which is a headline in itself.</summary>
    private const float PipWidth = 18f;

    private const float PipHeight = 22f;
    private const int PipGap = 4;

    /// <summary>Gap between two sections sharing a row.</summary>
    private const int SideBySideGap = 20;

    /// <summary>How wide a column-heading tip is drawn.</summary>
    private const float HeadingTipWidth = 470f;

    /// <summary>A card sizes its own preview; a relic's is assembled and needs a width.</summary>
    private const float CardPreviewWidth = 0f;

    /// <summary>
    /// Title (with a Show Graph button where there is something to plot) over the
    /// panelled table.
    /// </summary>
    public static Control BuildSection(TableSection section, Action<TableSection>? onShowGraph, HoverTip? tip)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);

        if (!string.IsNullOrEmpty(section.Title))
            column.AddChild(BuildHeaderRow(section, onShowGraph));

        column.AddChild(NativeStyle.Panel(BuildBody(section, tip)));
        return column;
    }

    private static Control BuildHeaderRow(TableSection section, Action<TableSection>? onShowGraph)
    {
        var header = NativeStyle.Header(section.Title);
        if (onShowGraph is null || !section.IsGraphable)
            return header;

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 24);
        // The heading takes the slack so the button sits against the right edge of every
        // section, rather than trailing whatever the title happens to be called.
        header.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(header);

        var graph = NativeStyle.TextButton("Show Graph", () => onShowGraph(section));
        row.AddChild(graph);
        return row;
    }

    private static Control BuildBody(TableSection section, HoverTip? tip)
    {
        var widths = MeasureColumns(section);
        var partWidths = MeasurePartWidths(section);
        var rowHeight = section.HasPips ? NativeStyle.ArtRowHeight : NativeStyle.RowHeight;
        var iconSize = section.HasPips ? TallRowIconSize : TextRowIconSize;

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 0);

        // A section that labels nothing — the pick tables, whose tab is their heading —
        // would still reserve the row's height for a line of empty strings.
        if (section.Columns.Any(column => !string.IsNullOrEmpty(column.Header)))
            body.AddChild(BuildHeaderCells(section, widths, tip));

        for (var i = 0; i < section.Rows.Count; i++)
            body.AddChild(BuildRow(
                section, section.Rows[i], widths, partWidths, rowHeight, iconSize, tip, striped: i % 2 == 0));

        return body;
    }

    /// <summary>
    /// The heading row. No rule under it and no background behind it: the first body row is
    /// striped, and that stripe is what separates the head from the body.
    /// </summary>
    private static Control BuildHeaderCells(TableSection section, float[] widths, HoverTip? tip)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", NativeStyle.ColumnSeparation);

        for (var i = 0; i < section.Columns.Count; i++)
        {
            var column = section.Columns[i];
            var cell = HeaderCell(column, tip);
            cell.CustomMinimumSize = new Vector2(widths[i], 0);
            cell.SizeFlagsHorizontal = i == 0 ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill;
            row.AddChild(cell);
        }

        var inset = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        inset.AddThemeConstantOverride("margin_bottom", NativeStyle.HeaderRowGap);
        inset.AddChild(row);
        return inset;
    }

    private static Control HeaderCell(TableColumn column, HoverTip? tip)
    {
        var label = NativeStyle.ColumnHeader(column.Header, column.RightAligned);

        // A heading with art in it — the character columns of the matrix — puts the icon on
        // the side the values are read from, so icon and numbers share an edge.
        Control content = column.Icon is null
            ? label
            : Beside(GameArt.IconSlot(column.Icon, HeaderIconSize), label, column.RightAligned);

        if (column.Tooltip is null || tip is null)
            return content;

        // A hairline under a heading is how the screen says there is more behind it. Solid
        // rather than dotted: Godot draws no dotted rule without a script, and at one pixel
        // and 40% cream the difference is not one anybody can see.
        var underlined = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        underlined.AddThemeConstantOverride("separation", 2);
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        underlined.AddChild(content);
        underlined.AddChild(new ColorRect
        {
            Color = NativeStyle.ColumnHeaderColor with { A = 0.4f },
            CustomMinimumSize = new Vector2(0, 1),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        var explanation = column.Tooltip;
        tip.Attach(
            underlined,
            () => HoverTip.Column(HoverTip.Paragraph(
                explanation, NativeStyle.CellColor, HoverTip.TextWidth(HeadingTipWidth))),
            HeadingTipWidth);
        return underlined;
    }

    private static Control BuildRow(
        TableSection section,
        IReadOnlyList<TableCell> cells,
        float[] widths,
        float[][] partWidths,
        int rowHeight,
        float iconSize,
        HoverTip? tip,
        bool striped)
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            // Row zero is striped, so the alternation itself draws the line under the head.
            BgColor = striped ? NativeStyle.ZebraColor : new Color(0, 0, 0, 0),
        });

        var row = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0, rowHeight),
        };
        row.AddThemeConstantOverride("separation", NativeStyle.ColumnSeparation);

        for (var i = 0; i < section.Columns.Count; i++)
        {
            var source = i < cells.Count ? cells[i] : Format.Empty;
            var cell = BuildCell(source, section.Columns[i], partWidths[i], iconSize);
            cell.CustomMinimumSize = new Vector2(widths[i], cell.CustomMinimumSize.Y);
            cell.SizeFlagsHorizontal = i == 0 ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill;
            row.AddChild(cell);
            AttachPreview(cell, source.Preview, tip);
        }

        panel.AddChild(row);
        return panel;
    }

    /// <summary>
    /// Let the name show the card or relic behind it.
    ///
    /// The name alone, not the whole row. A card is 300 px of picture and the rows are 33 px
    /// apart, so a row-wide target means the card is up whenever the cursor is anywhere in
    /// the table — including over the numbers it is covering. Held to the name, it appears
    /// when you point at the thing it is a picture of.
    ///
    /// Only cells that have something to show listen at all. A card the model database has
    /// never heard of — a mod's card in an old run file, say — leaves the row inert rather
    /// than opening an empty frame under the cursor.
    /// </summary>
    private static void AttachPreview(Control cell, string? key, HoverTip? tip)
    {
        if (tip is null || key is null || !GamePreview.Exists(key))
            return;

        var cards = key.StartsWith(ArtKey.CardPreviewPrefix, StringComparison.Ordinal);
        tip.Attach(
            cell,
            () => GamePreview.Of(key) ?? new Control(),
            cards ? CardPreviewWidth : GamePreview.RelicWidth,
            // The card arrives already framed, in the game's own border.
            framed: !cards);
    }

    private static Control BuildCell(TableCell cell, TableColumn column, float[] partWidths, float iconSize)
    {
        if (cell.Pips is { } pips)
            return PipStrip(pips);

        if (column.Bar is { } spec)
            return NativeStyle.ComparisonBar(cell.Bar, spec);

        if (cell.Parts.Count > 1)
            return PairedCell(cell, partWidths);

        var text = cell.Parts.Count == 0 ? Format.Empty : cell.Parts[0];
        var label = NativeStyle.Cell(text, column.RightAligned);
        label.VerticalAlignment = VerticalAlignment.Center;

        // The slot is reserved whether or not the game has art to put in it: a rarity with
        // no icon would otherwise pull its label left out of the column the others share.
        return cell.Icon is null
            ? label
            : Beside(GameArt.IconSlot(cell.Icon, iconSize), label, column.RightAligned);
    }

    /// <summary>
    /// An icon and its label, with the icon on the outside — left of a left-aligned label,
    /// left of a right-aligned one too, so the text still meets the column's right edge.
    /// </summary>
    private static Control Beside(Control icon, Control label, bool rightAligned)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", IconGap);
        if (rightAligned)
        {
            row.Alignment = BoxContainer.AlignmentMode.End;
            label.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        }
        row.AddChild(icon);
        row.AddChild(label);
        return row;
    }

    private static Control PipStrip(IReadOnlyList<bool> pips)
    {
        var row = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        row.AddThemeConstantOverride("separation", PipGap);
        foreach (var win in pips)
            row.AddChild(NativeStyle.Pip(win, PipWidth, PipHeight, lettered: false));
        return row;
    }

    private static Control PairedCell(TableCell cell, float[] partWidths)
    {
        var row = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            // The pair keeps its measured width and sits against the column's right edge.
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.End,
        };
        row.AddThemeConstantOverride("separation", NativeStyle.PartSeparation);

        for (var part = 0; part < cell.Parts.Count; part++)
        {
            var label = NativeStyle.Cell(cell.Parts[part], rightAligned: true);
            label.VerticalAlignment = VerticalAlignment.Center;
            // The second part is the rate the first part produced. Dimmed, so the record
            // reads first and the rate reads as a gloss on it rather than a rival figure.
            if (part > 0)
                label.AddThemeColorOverride("font_color", NativeStyle.CellColor with { A = 0.72f });
            // Fill, not ExpandFill: the measured width is the point, and expanding would
            // let each row size its own parts and break the alignment.
            label.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            label.CustomMinimumSize = new Vector2(part < partWidths.Length ? partWidths[part] : 0, 0);
            row.AddChild(label);
        }
        return row;
    }

    /// <summary>
    /// How wide each column has to be: the widest thing in it, heading included, with the
    /// column's own floor applied for the ones that draw rather than write.
    ///
    /// Measured once for the whole table and handed to every row. This is what replaces the
    /// grid: rows that each sized themselves would drift a pixel or two apart per row and
    /// the columns would fan out down the page.
    /// </summary>
    private static float[] MeasureColumns(TableSection section)
    {
        var widths = new float[section.Columns.Count];

        for (var i = 0; i < section.Columns.Count; i++)
        {
            var column = section.Columns[i];
            var width = Math.Max(column.MinWidth, NativeStyle.MeasureColumnHeader(column.Header));
            if (column.Icon is not null)
                width += HeaderIconSize + IconGap;
            widths[i] = width;
        }

        var iconSize = section.HasPips ? TallRowIconSize : TextRowIconSize;

        foreach (var row in section.Rows)
            for (var i = 0; i < section.Columns.Count && i < row.Count; i++)
            {
                // A bar or a pip strip has no text; its column said how much room it needs.
                if (section.Columns[i].Bar is not null || row[i].Pips is not null)
                    continue;
                widths[i] = Math.Max(widths[i], MeasureCell(row[i], iconSize));
            }

        return widths;
    }

    private static float MeasureCell(TableCell cell, float iconSize)
    {
        var width = 0f;
        for (var part = 0; part < cell.Parts.Count; part++)
        {
            width += NativeStyle.MeasureCell(cell.Parts[part]);
            if (part > 0)
                width += NativeStyle.PartSeparation;
        }
        if (cell.Icon is not null)
            width += iconSize + IconGap;
        return width;
    }

    /// <summary>
    /// The widest each part gets anywhere in its column, so a record and its rate line up
    /// down the page even though they share one cell. Measured with the real font rather
    /// than estimated from character counts, because the game's numerals are not the same
    /// width as its letters.
    /// </summary>
    private static float[][] MeasurePartWidths(TableSection section)
    {
        var widths = new float[section.Columns.Count][];
        for (var column = 0; column < section.Columns.Count; column++)
        {
            var parts = section.Rows.Count == 0
                ? 1
                : section.Rows.Max(row => column < row.Count ? row[column].Parts.Count : 1);
            widths[column] = new float[Math.Max(parts, 1)];

            if (parts <= 1)
                continue;

            foreach (var row in section.Rows)
            {
                if (column >= row.Count)
                    continue;
                var cell = row[column];
                for (var part = 0; part < cell.Parts.Count && part < parts; part++)
                    widths[column][part] = Math.Max(
                        widths[column][part],
                        NativeStyle.MeasureCell(cell.Parts[part]));
            }
        }
        return widths;
    }

    /// <summary>All of a tab's sections, separated the way the native screen separates blocks.</summary>
    public static Control BuildTab(
        IReadOnlyList<TableSection> sections,
        string emptyMessage,
        Action<TableSection>? onShowGraph = null,
        HoverTip? tip = null)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", NativeStyle.SectionSeparation);

        if (sections.Count == 0)
        {
            column.AddChild(NativeStyle.Header(emptyMessage));
            return column;
        }

        foreach (var section in sections)
            column.AddChild(section.Beside is null
                ? BuildSection(section, onShowGraph, tip)
                : SideBySide(section, onShowGraph, tip));

        return column;
    }

    /// <summary>Two narrow tables sharing a row, each taking half the width.</summary>
    private static Control SideBySide(TableSection section, Action<TableSection>? onShowGraph, HoverTip? tip)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", SideBySideGap);

        foreach (var half in new[] { section, section.Beside! })
        {
            var built = BuildSection(half, onShowGraph, tip);
            built.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(built);
        }
        return row;
    }
}

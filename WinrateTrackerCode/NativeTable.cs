using Godot;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Renders a <see cref="TableSection" /> as native controls.
///
/// One <see cref="GridContainer" /> holds the column headers and every cell, so a column
/// is exactly as wide as its widest entry and the rows cannot fall out of step. Numeric
/// columns carry the expand flag and right-align, which is what puts their digits against
/// a shared right edge — the thing that makes a dense table scannable.
/// </summary>
internal static class NativeTable
{
    /// <summary>
    /// Title (with a Show Graph button where there is something to plot) over the
    /// panelled table.
    /// </summary>
    public static Control BuildSection(TableSection section, Action<TableSection>? onShowGraph)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);

        if (!string.IsNullOrEmpty(section.Title))
            column.AddChild(BuildHeaderRow(section, onShowGraph));

        column.AddChild(NativeStyle.Panel(BuildGrid(section)));
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
        row.AddChild(NativeStyle.TextButton("Show Graph", () => onShowGraph(section)));
        return row;
    }

    private static GridContainer BuildGrid(TableSection section)
    {
        var grid = new GridContainer { Columns = section.Columns.Count };
        grid.AddThemeConstantOverride("h_separation", NativeStyle.ColumnSeparation);
        grid.AddThemeConstantOverride("v_separation", NativeStyle.RowSeparation);

        var partWidths = MeasurePartWidths(section);

        // A header row of empty strings would still reserve its height, so a section that
        // labels nothing (the Overview summary) skips the row entirely.
        if (section.Columns.Any(column => !string.IsNullOrEmpty(column.Header)))
            foreach (var column in section.Columns)
                grid.AddChild(NativeStyle.Cell(column.Header, column.RightAligned, header: true));

        foreach (var row in section.Rows)
            for (var i = 0; i < section.Columns.Count; i++)
                grid.AddChild(BuildCell(
                    i < row.Count ? row[i] : Format.Empty,
                    section.Columns[i].RightAligned,
                    partWidths[i]));

        return grid;
    }

    /// <summary>
    /// The widest each part gets anywhere in its column, so a record and its rate line up
    /// down the page even though they share one grid cell. Measured with the real font
    /// rather than estimated from character counts, because the game's numerals are not
    /// the same width as its letters.
    /// </summary>
    private static float[][] MeasurePartWidths(TableSection section)
    {
        var widths = new float[section.Columns.Count][];
        for (var column = 0; column < section.Columns.Count; column++)
        {
            var parts = section.Rows.Count == 0
                ? 1
                : section.Rows.Max(row => column < row.Count ? row[column].Parts.Count : 1);
            widths[column] = new float[parts];

            if (parts == 1)
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

    private static Control BuildCell(TableCell cell, bool rightAligned, float[] partWidths)
    {
        if (cell.Parts.Count <= 1)
            return NativeStyle.Cell(cell.Parts.Count == 0 ? Format.Empty : cell.Parts[0], rightAligned);

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
            // Fill, not ExpandFill: the measured width is the point, and expanding would
            // let each row size its own parts and break the alignment.
            label.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            label.CustomMinimumSize = new Vector2(part < partWidths.Length ? partWidths[part] : 0, 0);
            row.AddChild(label);
        }
        return row;
    }

    /// <summary>All of a tab's sections, separated the way the native screen separates blocks.</summary>
    public static Control BuildTab(
        IReadOnlyList<TableSection> sections,
        string emptyMessage,
        Action<TableSection>? onShowGraph = null)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", NativeStyle.SectionSeparation);

        if (sections.Count == 0)
        {
            column.AddChild(NativeStyle.Header(emptyMessage));
            return column;
        }

        foreach (var section in sections)
            column.AddChild(BuildSection(section, onShowGraph));

        return column;
    }
}

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

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 24);
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

        // A group row sits above the column headers, naming spans of columns.
        if (section.GroupHeaders is { } groups)
            for (var i = 0; i < section.Columns.Count; i++)
                grid.AddChild(NativeStyle.GroupHeaderCell(
                    i < groups.Count ? groups[i] : "",
                    section.Columns[i].RightAligned));

        // A header row of empty strings would still reserve its height, so a section that
        // labels nothing (the Overview summary) skips the row entirely.
        if (section.Columns.Any(column => !string.IsNullOrEmpty(column.Header)))
            foreach (var column in section.Columns)
                grid.AddChild(NativeStyle.Cell(column.Header, column.RightAligned, header: true));

        foreach (var row in section.Rows)
            for (var i = 0; i < section.Columns.Count; i++)
                grid.AddChild(NativeStyle.Cell(
                    i < row.Count ? row[i] : Format.Empty,
                    section.Columns[i].RightAligned));

        return grid;
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

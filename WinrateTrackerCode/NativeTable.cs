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
    /// <summary>Title, panelled table, and optional caveat, stacked.</summary>
    public static Control BuildSection(TableSection section)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);

        if (!string.IsNullOrEmpty(section.Title))
            column.AddChild(NativeStyle.Header(section.Title));

        column.AddChild(NativeStyle.Panel(BuildGrid(section)));

        if (!string.IsNullOrEmpty(section.Note))
            column.AddChild(NativeStyle.Note(section.Note));

        return column;
    }

    private static GridContainer BuildGrid(TableSection section)
    {
        var grid = new GridContainer { Columns = section.Columns.Count };
        grid.AddThemeConstantOverride("h_separation", NativeStyle.ColumnSeparation);
        grid.AddThemeConstantOverride("v_separation", NativeStyle.RowSeparation);

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
    public static Control BuildTab(IReadOnlyList<TableSection> sections, string emptyMessage)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", NativeStyle.SectionSeparation);

        if (sections.Count == 0)
        {
            column.AddChild(NativeStyle.Header(emptyMessage));
            return column;
        }

        foreach (var section in sections)
            column.AddChild(BuildSection(section));

        return column;
    }
}

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// How a column's comparison bar is drawn: what the notch marks, how wide the track is in
/// the same units as the value, and whether the fill grows from the notch or from zero.
///
/// The spec lives on the column rather than on each cell because it is the same claim for
/// the whole column — "this is where your average sits, and this is how far the track
/// runs". Only the value differs per row, and that is on <see cref="TableCell.Bar" />.
/// </summary>
/// <param name="Baseline">Where the notch sits, in the value's own units (0-1 win rate).</param>
/// <param name="Scale">The value at the right-hand end of the track.</param>
/// <param name="Signed">
/// When true the fill runs between the notch and the value, so a below-average row grows
/// leftwards and direction alone reads the comparison. When false it runs from zero, and
/// only the length does.
/// </param>
internal sealed record BarSpec(double Baseline, double Scale, bool Signed = false);

/// <summary>
/// A column heading, and how the values under it are laid out.
/// </summary>
/// <param name="Header">Heading text. The renderer sets it in caps.</param>
/// <param name="RightAligned">Whether the values are numbers that line up on the right.</param>
internal sealed record TableColumn(string Header, bool RightAligned = false)
{
    /// <summary>Set when this column draws comparison bars rather than text.</summary>
    public BarSpec? Bar { get; init; }

    /// <summary>
    /// A floor on the column's width. Bars and pips have no text to measure, so they say
    /// how much room they need; text columns are measured from their contents.
    /// </summary>
    public float MinWidth { get; init; }

    /// <summary>
    /// What the heading means, shown as a hover tip. A column with one is underlined, the
    /// way the game marks anything with a tip behind it.
    /// </summary>
    public string? Tooltip { get; init; }

    /// <summary>Art shown before the heading — a character icon over its own column.</summary>
    public string? Icon { get; init; }
}

/// <summary>
/// One cell. Usually a single value, but a column can carry two related numbers — a
/// record and its rate — as separate parts, or a bar, an icon, or a strip of win/loss pips
/// instead of text.
///
/// Parts are not separate columns on purpose. A column gap is sized to separate one
/// heading's worth of data from the next, and putting a record and its own rate either
/// side of that gap spends it in the wrong place: the two look as far apart as two
/// different characters do. As parts they sit under one heading, tight together, and
/// still line up down the page because the renderer measures each part across the whole
/// column.
/// </summary>
internal sealed record TableCell(IReadOnlyList<string> Parts)
{
    public static implicit operator TableCell(string text) => new([text]);

    public static TableCell Pair(string first, string second) => new([first, second]);

    /// <summary>The value this row's comparison bar draws, against its column's <see cref="BarSpec" />.</summary>
    public double? Bar { get; init; }

    /// <summary>An <see cref="ArtKey" /> drawn before the text — a character or a rarity.</summary>
    public string? Icon { get; init; }

    /// <summary>Ten runs as wins and losses, oldest first, drawn as pips instead of text.</summary>
    public IReadOnlyList<bool>? Pips { get; init; }

    /// <summary>The whole cell as one string. For tests and diagnostics.</summary>
    public string Text => string.Join(' ', Parts);
}

/// <summary>
/// One period as the graph plots it: a bar for the wins, a point on the line for the
/// all-time rate as of that period's end. Oldest first, which is left to right.
/// </summary>
internal sealed record SeriesPoint(string Label, int Wins, int Runs, double CumulativeWinRate);

/// <summary>A titled block of rows. One or more of these makes a tab.</summary>
internal sealed record TableSection(
    string Title,
    IReadOnlyList<TableColumn> Columns,
    IReadOnlyList<IReadOnlyList<TableCell>> Rows,
    IReadOnlyList<SeriesPoint>? Series = null)
{
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>Whether this table has something worth plotting.</summary>
    public bool IsGraphable => Series is { Count: > 1 };

    /// <summary>
    /// Rows carrying art need more height than rows of text. Asked of the section rather
    /// than set by hand so a table cannot be given icons and left at the text row height.
    /// </summary>
    public bool HasArt =>
        Columns.Any(column => column.Icon is not null)
        || Rows.Any(row => row.Any(cell => cell.Icon is not null || cell.Pips is not null));

    /// <summary>
    /// Whether a row has to be tall enough for a strip of pips.
    ///
    /// Only pips force it. An icon beside a label fits a text row perfectly well at a
    /// smaller size, and giving the pick tables — which run to hundreds of rows — the taller
    /// height for the sake of a rarity icon would cost a screenful of scrolling for nothing.
    /// </summary>
    public bool HasPips => Rows.Any(row => row.Any(cell => cell.Pips is not null));

    /// <summary>
    /// Two sections shown side by side rather than stacked. The two time-of-day tables are
    /// narrow and about the same question, so a full-width row each wastes the screen.
    /// </summary>
    public TableSection? Beside { get; init; }
}

/// <summary>The tabs, in the order they appear.</summary>
internal enum ReportTab
{
    Home,
    Splits,
    Characters,
    Cards,
    Relics,
}

/// <summary>
/// Turns a <see cref="WinrateReport" /> into the exact text of every table.
///
/// Deliberately separate from the Godot rendering: what a cell says is a question about
/// the report, and keeping it here means the whole screen's contents can be asserted in
/// tests without launching the game. The renderer's only job is where the text goes.
///
/// The Home tab is not a table and is built by <see cref="HomePanel" />, on the same terms
/// and for the same reason.
/// </summary>
internal static class ReportTables
{
    public static string Title(ReportTab tab) => tab switch
    {
        ReportTab.Home => "Home",
        ReportTab.Splits => "Splits",
        ReportTab.Characters => "Characters",
        ReportTab.Cards => "Cards",
        ReportTab.Relics => "Relics",
        _ => tab.ToString(),
    };

    /// <summary>
    /// What the bar column's heading says, and the tip behind it. The player's own rate is
    /// in the heading because a bar with an unlabelled notch is a comparison against
    /// nothing.
    /// </summary>
    private static TableColumn ComparisonColumn(double allTimeRate, float width) =>
        new($"vs your avg {Format.Percent(allTimeRate)}")
        {
            Bar = new BarSpec(allTimeRate, ComparisonScale),
            MinWidth = width,
            Tooltip =
                $"Bars run from zero on a 0–{Format.WholePercent(ComparisonScale)} scale, "
                + $"with the notch at your {Format.Percent(allTimeRate)} all-time rate. "
                + "Longer than the notch means that period beat your average.",
        };

    /// <summary>
    /// The right-hand end of the unsigned comparison track. Fixed rather than fitted to the
    /// best row: a track that rescales itself makes two visits to the same table disagree
    /// about how good a month was.
    /// </summary>
    private const double ComparisonScale = 0.40d;

    /// <summary>
    /// <paramref name="picks" /> narrows the Cards and Relics tabs only. It is a separate
    /// argument rather than part of the report because it hides rows, not runs — the win
    /// rate of a card must not change according to which other cards are on screen.
    /// </summary>
    public static IReadOnlyList<TableSection> Build(
        ReportTab tab,
        WinrateReport report,
        PickFilter? picks = null) => tab switch
    {
        // Home is not a table; see HomePanel.
        ReportTab.Home => [],
        ReportTab.Splits => Splits(report),
        ReportTab.Characters => Characters(report),
        ReportTab.Cards => Picks(report, picks ?? PickFilter.Default, ReportTab.Cards),
        ReportTab.Relics => Picks(report, picks ?? PickFilter.Default, ReportTab.Relics),
        _ => [],
    };

    /// <summary>
    /// Every way of cutting the archive up: first the consecutive stretches — by month, by
    /// patch, by fixed-size blocks — which are all the same shape and so all graph, then
    /// the two cuts by time of day, which are not stretches of time and do not.
    ///
    /// 10-run blocks are not here: the Home trend covers that granularity, and a second
    /// table saying the same thing at more length is a table nobody reads.
    /// </summary>
    private static IReadOnlyList<TableSection> Splits(WinrateReport report)
    {
        if (report.IsEmpty)
            return [];

        var rate = report.Overall.WinRate;
        return
        [
            // The month is the date, so a from and a to beside it say nothing the label
            // has not already said.
            PeriodSection("By month", report.Months, "month", rate, withFloors: true, withDates: false),
            PeriodSection("By patch", report.Patches, "patch", rate),
            PeriodSection("50-run blocks", report.Blocks50, "block", rate),
            GroupSection("By time of day", report.TimeOfDay, "time") with
            {
                Beside = GroupSection("Every 4 hours", report.HourBlocks, "hours"),
            },
        ];
    }

    /// <summary>
    /// A table of named buckets. No from/to and no cumulative column: these rows are not
    /// consecutive, so a running total across them would mean nothing.
    /// </summary>
    private static TableSection GroupSection(string title, IReadOnlyList<GroupRow> groups, string labelHeader) =>
        new(
            title,
            [
                new TableColumn(labelHeader),
                new TableColumn("runs", RightAligned: true),
                new TableColumn("record", RightAligned: true),
                new TableColumn("win%", RightAligned: true),
            ],
            groups.Select(group => (IReadOnlyList<TableCell>)
            [
                group.Label,
                Format.Count(group.Tally.Runs),
                Format.WinLoss(group.Tally),
                Format.Percent(group.Tally),
            ]).ToList());

    private static TableSection PeriodSection(
        string title,
        IReadOnlyList<PeriodRow> periods,
        string labelHeader,
        double allTimeRate,
        bool withFloors = false,
        bool withDates = true)
    {
        List<TableColumn> columns = [new(labelHeader)];
        if (withDates)
        {
            columns.Add(new TableColumn("from", RightAligned: true));
            columns.Add(new TableColumn("to", RightAligned: true));
        }
        columns.Add(new TableColumn("record", RightAligned: true));
        columns.Add(new TableColumn("win%", RightAligned: true));
        columns.Add(ComparisonColumn(allTimeRate, PeriodBarWidth));
        columns.Add(new TableColumn("cumulative%", RightAligned: true));
        if (withFloors)
            columns.Add(new TableColumn("avg floors", RightAligned: true));

        var rows = periods.Select(period =>
        {
            List<TableCell> cells = [period.Label];
            if (withDates)
            {
                cells.Add(Format.ShortDate(period.From));
                cells.Add(Format.ShortDate(period.To));
            }
            cells.Add(Format.WinLoss(period.Tally));
            cells.Add(Format.WholePercent(period.Tally));
            cells.Add(new TableCell([]) { Bar = period.Tally.Runs == 0 ? null : period.Tally.WinRate });
            cells.Add(Format.Percent(period.CumulativeWinRate));
            if (withFloors)
                cells.Add(Format.Average(period.AverageFloors));
            return (IReadOnlyList<TableCell>)cells;
        }).ToList();

        // The table reads newest first; the graph reads left to right through time.
        var series = periods
            .Reverse()
            .Select(period => new SeriesPoint(period.Label, period.Tally.Wins, period.Tally.Runs, period.CumulativeWinRate))
            .ToList();

        return new TableSection(title, columns, rows, series);
    }

    private const float PeriodBarWidth = 210f;
    private const float PickBarWidth = 260f;

    private static IReadOnlyList<TableSection> Characters(WinrateReport report)
    {
        if (report.IsEmpty)
            return [];

        var byCharacter = new TableSection(
            "By character",
            [
                new TableColumn("character"),
                new TableColumn("runs", RightAligned: true),
                new TableColumn("all time", RightAligned: true),
                new TableColumn("last 50", RightAligned: true),
                ComparisonColumn(report.Overall.WinRate, PeriodBarWidth),
                new TableColumn("last 10") { MinWidth = PipStripWidth },
            ],
            report.Characters.Select(row => (IReadOnlyList<TableCell>)
            [
                new TableCell([row.Character]) { Icon = ArtKey.Character(row.Character) },
                Format.Count(row.All.Runs),
                TableCell.Pair(Format.WinLoss(row.All), Format.WholePercent(row.All)),
                TableCell.Pair(Format.WinLoss(row.Last50), Format.WholePercent(row.Last50)),
                // The bar reads the recent rate, not the career one: the question the row
                // is asking is how this character is going now.
                new TableCell([]) { Bar = row.Last50.Runs == 0 ? null : row.Last50.WinRate },
                new TableCell([]) { Pips = row.RecentRuns },
            ]).ToList());

        // One column per character, its record and rate paired inside it.
        var columns = new List<TableColumn> { new("month") };
        columns.AddRange(report.MatrixCharacters.Select(character =>
            new TableColumn(character, RightAligned: true) { Icon = ArtKey.Character(character) }));

        var matrix = new TableSection(
            "Character by month",
            columns,
            report.MonthByCharacter.Select(row =>
            {
                var cells = new List<TableCell> { row.Label };
                foreach (var tally in row.Cells)
                    cells.Add(tally is { } played
                        ? TableCell.Pair(Format.WinLoss(played), Format.WholePercent(played))
                        : TableCell.Pair(Format.Empty, ""));
                return (IReadOnlyList<TableCell>)cells;
            }).ToList());

        return [byCharacter, matrix];
    }

    /// <summary>Ten pips and the gaps between them; the column has no text to measure.</summary>
    private const float PipStripWidth = 216f;

    /// <summary>
    /// What picking each card, or each relic, was worth.
    ///
    /// Every pick the filtered runs made, best win rate first. The starting deck and
    /// starting relic are not picks and are left out upstream, in
    /// <see cref="RunRecord.PickedCards" />.
    ///
    /// The pick count sits beside the rate on purpose. Sorted by rate alone the head of the
    /// list is whatever was picked once and won, so the column that says how many runs are
    /// behind a number has to be right there next to it.
    ///
    /// The table carries no heading of its own: the tab is already named for it, and a
    /// second "Cards" in gold under the Cards tab says nothing.
    /// </summary>
    private static IReadOnlyList<TableSection> Picks(WinrateReport report, PickFilter filter, ReportTab tab)
    {
        if (report.IsEmpty)
            return [];

        var cards = tab == ReportTab.Cards;
        var section = PickSection(
            cards ? "card" : "relic",
            cards ? filter.ApplyToCards(report.Cards) : filter.ApplyToRelics(report.Relics),
            cards ? GameData.CardName : GameData.RelicName,
            cards ? GameData.Cards : GameData.Relics,
            report.Overall.WinRate);

        // Runs from before the mod could read decks, a run filter that leaves only such
        // runs, or a minimum that nothing clears: all of them can empty the list, and the
        // tab should then say so rather than draw an empty frame.
        return section.IsEmpty ? [] : [section];
    }

    private static TableSection PickSection(
        string labelHeader,
        IReadOnlyList<PickRow> picks,
        Func<string, string> nameOf,
        string table,
        double allTimeRate) =>
        new(
            "",
            [
                new TableColumn(labelHeader),
                new TableColumn("rarity"),
                new TableColumn("picked", RightAligned: true),
                new TableColumn("record", RightAligned: true),
                new TableColumn("win%", RightAligned: true),
                SignedComparisonColumn(allTimeRate),
            ],
            picks.Select(pick => (IReadOnlyList<TableCell>)
            [
                nameOf(pick.Id),
                new TableCell([pick.Rarity]) { Icon = ArtKey.Rarity(table, pick.Rarity) },
                Format.Count(pick.Tally.Runs),
                Format.WinLoss(pick.Tally),
                Format.Percent(pick.Tally),
                new TableCell([]) { Bar = pick.Tally.Runs == 0 ? null : pick.Tally.WinRate },
            ]).ToList());

    /// <summary>
    /// The picks bar, which runs both ways from the player's own rate.
    ///
    /// A pick is worth taking relative to how the player does anyway, so the interesting
    /// quantity is the distance from that rate and which side of it the pick falls. The
    /// track spans the whole 0-100% so the notch sits where the rate actually is, and a
    /// near-average pick draws nearly nothing — which is the truth about it.
    /// </summary>
    private static TableColumn SignedComparisonColumn(double allTimeRate) =>
        new($"vs your avg {Format.Percent(allTimeRate)}")
        {
            Bar = new BarSpec(allTimeRate, 1.0d, Signed: true),
            MinWidth = PickBarWidth,
            Tooltip =
                $"Bars run from your {Format.Percent(allTimeRate)} all-time rate: right when a pick "
                + "beat it, left when it did not. A near-average pick draws almost nothing.",
        };
}

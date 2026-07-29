using System.Globalization;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// How every number in the report is written.
///
/// Shared rather than inlined at each call site so the four tabs cannot drift into
/// showing the same quantity two ways. Invariant culture throughout: these are compact
/// numeric columns that have to line up against a fixed right edge, and a locale that
/// swaps the decimal separator or pads differently would break the alignment the tables
/// depend on.
/// </summary>
internal static class Format
{
    /// <summary>Stands in for a value that does not exist, rather than a misleading 0.</summary>
    public const string Empty = "—";

    /// <summary>e.g. <c>41.3%</c>.</summary>
    public static string Percent(double rate) =>
        (rate * 100d).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    /// <summary>Win rate, or <see cref="Empty" /> when there is nothing to divide.</summary>
    public static string Percent(Tally tally) => tally.Runs == 0 ? Empty : Percent(tally.WinRate);

    /// <summary>e.g. <c>83-264</c>.</summary>
    public static string WinLoss(Tally tally) =>
        tally.Runs == 0 ? Empty : $"{tally.Wins}-{tally.Losses}";

    /// <summary>
    /// Win rate in whole percent, e.g. <c>26%</c>.
    ///
    /// Used where the rate sits in its own column beside the record it came from. One
    /// decimal is reserved for the Overview, where a single rate is the headline and worth
    /// the precision; across a grid of columns a decimal point is width spent on digits
    /// nobody compares.
    /// </summary>
    public static string WholePercent(Tally tally) =>
        tally.Runs == 0 ? Empty : (tally.WinRate * 100d).ToString("0", CultureInfo.InvariantCulture) + "%";

    public static string WholePercent(double rate) =>
        (rate * 100d).ToString("0", CultureInfo.InvariantCulture) + "%";

    public static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>One decimal place, for averages.</summary>
    public static string Average(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>Two decimal places, for average act reached where the spread is small.</summary>
    public static string AverageAct(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Whole minutes.</summary>
    public static string Minutes(double value) => value.ToString("0", CultureInfo.InvariantCulture);

    /// <summary><c>MM-dd</c>. Block tables carry two of these per row, so the year is dropped.</summary>
    public static string ShortDate(DateTime value) => value.ToString("MM-dd", CultureInfo.InvariantCulture);

    /// <summary><c>yyyy-MM-dd</c>, for the one place a full date is shown.</summary>
    public static string Date(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary><c>2026-07</c> becomes <c>Jul 2026</c>.</summary>
    public static string MonthName(string month)
    {
        if (DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        return month;
    }

    /// <summary><c>2026-07</c> becomes <c>Jul</c>, for matrix column headings.</summary>
    public static string MonthAbbreviation(string month)
    {
        if (DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed.ToString("MMM", CultureInfo.InvariantCulture);
        return month;
    }

    /// <summary>e.g. <c>3 wins</c> / <c>2 losses</c> / <c>none</c>.</summary>
    public static string Streak(int length, bool isWin)
    {
        if (length == 0)
            return "none";
        var noun = isWin
            ? length == 1 ? "win" : "wins"
            : length == 1 ? "loss" : "losses";
        return $"{length} {noun}";
    }
}

using WinrateTracker.WinrateTrackerCode;
using Xunit;

namespace WinrateTracker.Tests;

public class FormatTests
{
    [Theory]
    [InlineData(0d, "0.0%")]
    [InlineData(0.413d, "41.3%")]
    [InlineData(1d, "100.0%")]
    [InlineData(0.2392d, "23.9%")]
    public void Rates_print_to_one_decimal(double rate, string expected) =>
        Assert.Equal(expected, Format.Percent(rate));

    [Fact]
    public void An_empty_tally_prints_a_dash_rather_than_a_misleading_zero()
    {
        var empty = new Tally(0, 0);

        Assert.Equal(Format.Empty, Format.Percent(empty));
        Assert.Equal(Format.Empty, Format.WinLoss(empty));
        Assert.Equal(Format.Empty, Format.WholePercent(empty));
    }

    [Fact]
    public void A_tally_prints_as_wins_then_losses()
    {
        Assert.Equal("83-264", Format.WinLoss(new Tally(347, 83)));
    }

    [Theory]
    [InlineData(50, 13, "26%")]
    [InlineData(347, 83, "24%")]
    [InlineData(4, 4, "100%")]
    [InlineData(4, 0, "0%")]
    public void A_rate_column_reads_in_whole_percent(int runs, int wins, string expected) =>
        Assert.Equal(expected, Format.WholePercent(new Tally(runs, wins)));

    [Fact]
    public void The_overview_keeps_one_decimal_where_a_single_rate_is_the_headline()
    {
        var tally = new Tally(347, 83);

        Assert.Equal("23.9%", Format.Percent(tally));
        Assert.Equal("24%", Format.WholePercent(tally));
    }

    [Theory]
    [InlineData(0, true, "none")]
    [InlineData(1, true, "1 win")]
    [InlineData(3, true, "3 wins")]
    [InlineData(1, false, "1 loss")]
    [InlineData(4, false, "4 losses")]
    public void Streaks_read_as_a_phrase(int length, bool isWin, string expected) =>
        Assert.Equal(expected, Format.Streak(length, isWin));

    [Fact]
    public void Dates_print_short_in_tables_and_long_on_their_own()
    {
        var date = new DateTime(2026, 7, 28);

        Assert.Equal("07-28", Format.ShortDate(date));
        Assert.Equal("2026-07-28", Format.Date(date));
    }

    [Theory]
    [InlineData("2026-07", "Jul 2026", "Jul")]
    [InlineData("2026-01", "Jan 2026", "Jan")]
    public void Months_read_as_names(string month, string full, string abbreviation)
    {
        Assert.Equal(full, Format.MonthName(month));
        Assert.Equal(abbreviation, Format.MonthAbbreviation(month));
    }

    [Fact]
    public void An_unparseable_month_is_passed_through_unchanged()
    {
        Assert.Equal("whenever", Format.MonthName("whenever"));
        Assert.Equal("whenever", Format.MonthAbbreviation("whenever"));
    }
}

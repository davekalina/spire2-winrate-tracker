using WinrateTracker.WinrateTrackerCode;
using Xunit;

namespace WinrateTracker.Tests;

public class ExampleTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 6)]
    [InlineData(-2, -4)]
    public void Double_returns_twice_the_input(int input, int expected) =>
        Assert.Equal(expected, Example.Double(input));
}

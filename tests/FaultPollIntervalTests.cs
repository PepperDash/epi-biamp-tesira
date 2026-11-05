using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class FaultPollIntervalTests
{
    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 10)]
    [InlineData(9, 10)]
    [InlineData(10, 10)]
    [InlineData(60, 60)]
    [InlineData(86400, 86400)]
    [InlineData(86401, 86400)]
    [InlineData(3000000, 86400)]
    [InlineData(int.MaxValue, 86400)]
    public void Normalize_Clamps_To_Ten_Seconds_Through_One_Day(int requested, int expected)
    {
        FaultPollInterval.Normalize(requested).Should().Be(expected);
    }

    [Fact]
    public void The_Maximum_Fits_The_Timer()
    {
        // System.Timers.Timer rejects an interval above int.MaxValue milliseconds.
        ((double)FaultPollInterval.MaximumSecs * 1000).Should().BeLessThanOrEqualTo(int.MaxValue);
    }
}

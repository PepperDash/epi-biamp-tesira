using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class RetryBackoffTests
{
    private static int[] DueTicks(RetryBackoff backoff, int ticks) =>
        Enumerable.Range(1, ticks).Where(_ => backoff.Tick()).ToArray();

    [Fact]
    public void Doubles_The_Gap_Up_To_The_Maximum()
    {
        // With 30 s watchdog ticks and max 10: retries at 30 s, 90 s, 210 s, 450 s, then every 300 s.
        DueTicks(new RetryBackoff(10), 45).Should().Equal(1, 3, 7, 15, 25, 35, 45);
    }

    [Fact]
    public void Reset_Starts_Over()
    {
        var backoff = new RetryBackoff(10);
        DueTicks(backoff, 7);

        backoff.Reset();

        backoff.Tick().Should().BeTrue();
        backoff.Tick().Should().BeFalse();
        backoff.Tick().Should().BeTrue();
    }

    [Fact]
    public void A_Maximum_Below_One_Retries_Every_Tick()
    {
        DueTicks(new RetryBackoff(0), 4).Should().Equal(1, 2, 3, 4);
    }
}

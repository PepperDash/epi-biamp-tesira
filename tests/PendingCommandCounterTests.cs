using FluentAssertions;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class PendingCommandCounterTests
{
    private readonly object fader = new();
    private readonly object other = new();

    [Fact]
    public void Tracks_Each_Key_Separately()
    {
        var counter = new PendingCommandCounter();
        counter.Add(fader);

        counter.Has(fader).Should().BeTrue();
        counter.Has(other).Should().BeFalse();
    }

    [Fact]
    public void Stays_Pending_Until_Every_Add_Is_Removed()
    {
        var counter = new PendingCommandCounter();
        counter.Add(fader);
        counter.Add(fader);

        counter.Remove(fader);
        counter.Has(fader).Should().BeTrue();

        counter.Remove(fader);
        counter.Has(fader).Should().BeFalse();
    }

    [Fact]
    public void Remove_Without_Add_Is_Ignored()
    {
        var counter = new PendingCommandCounter();
        counter.Remove(fader);
        counter.Add(fader);

        counter.Has(fader).Should().BeTrue();
    }

    [Fact]
    public void Clear_Drops_Everything()
    {
        var counter = new PendingCommandCounter();
        counter.Add(fader);
        counter.Add(other);

        counter.Clear();

        counter.Has(fader).Should().BeFalse();
        counter.Has(other).Should().BeFalse();
    }
}

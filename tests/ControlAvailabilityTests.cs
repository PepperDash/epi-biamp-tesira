using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class ControlAvailabilityTests
{
    [Fact]
    public void Reports_Becoming_Unavailable_Once()
    {
        var availability = new ControlAvailability();

        availability.MarkUnavailable("dsp--BlockA").Should().BeTrue();
        availability.MarkUnavailable("dsp--BlockA").Should().BeFalse();
        availability.Count.Should().Be(1);
    }

    [Fact]
    public void Reports_Answering_Again_Only_If_It_Was_Unavailable()
    {
        var availability = new ControlAvailability();

        availability.MarkAvailable("dsp--BlockA").Should().BeFalse();
        availability.MarkUnavailable("dsp--BlockA");
        availability.MarkAvailable("dsp--BlockA").Should().BeTrue();
        availability.MarkAvailable("dsp--BlockA").Should().BeFalse();
        availability.Count.Should().Be(0);
    }

    [Fact]
    public void Lists_Keys_Sorted_And_Ignores_Empty_Keys()
    {
        var availability = new ControlAvailability();

        availability.MarkUnavailable("dsp--B");
        availability.MarkUnavailable("dsp--A");
        availability.MarkUnavailable("").Should().BeFalse();
        availability.MarkUnavailable(null!).Should().BeFalse();

        availability.Keys.Should().Equal("dsp--A", "dsp--B");
    }
}

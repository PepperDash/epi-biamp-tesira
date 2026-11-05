using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class ControlAvailabilityTests
{
    [Fact]
    public void Reports_Each_Target_Becoming_Unavailable_Once()
    {
        var availability = new ControlAvailability();

        availability.MarkUnavailable("dsp--Fader1", "Level1").Should().BeTrue();
        availability.MarkUnavailable("dsp--Fader1", "Level1").Should().BeFalse();
        availability.MarkUnavailable("dsp--Fader1", "Mute1").Should().BeTrue();
        availability.Count.Should().Be(1);
        availability.TargetsOf("dsp--Fader1").Should().Equal("Level1", "Mute1");
    }

    [Fact]
    public void Stays_Unavailable_Until_Every_Unavailable_Target_Answers()
    {
        // A fader whose mute block is on a down unit while its level block answers.
        var availability = new ControlAvailability();
        availability.MarkUnavailable("dsp--Fader1", "Mute1");

        availability.MarkAvailable("dsp--Fader1", "Level1", out var levelRecovered).Should().BeFalse();
        levelRecovered.Should().BeFalse();
        availability.Contains("dsp--Fader1").Should().BeTrue();

        availability.MarkAvailable("dsp--Fader1", "Mute1", out var muteRecovered).Should().BeTrue();
        muteRecovered.Should().BeTrue();
        availability.Contains("dsp--Fader1").Should().BeFalse();
    }

    [Fact]
    public void Reports_A_Target_Recovering_While_Another_Is_Still_Down()
    {
        var availability = new ControlAvailability();
        availability.MarkUnavailable("dsp--Fader1", "Level1");
        availability.MarkUnavailable("dsp--Fader1", "Mute1");

        availability.MarkAvailable("dsp--Fader1", "Level1", out var recovered).Should().BeFalse();

        recovered.Should().BeTrue();
        availability.TargetsOf("dsp--Fader1").Should().Equal("Mute1");
    }

    [Fact]
    public void A_Successful_Reply_From_An_Available_Control_Changes_Nothing()
    {
        var availability = new ControlAvailability();

        availability.MarkAvailable("dsp--Fader1", "Level1", out var recovered).Should().BeFalse();
        recovered.Should().BeFalse();
        availability.Count.Should().Be(0);
    }

    [Fact]
    public void Lists_Keys_Sorted_And_Ignores_Empty_Keys()
    {
        var availability = new ControlAvailability();

        availability.MarkUnavailable("dsp--B", "B");
        availability.MarkUnavailable("dsp--A", "A");
        availability.MarkUnavailable("", "X").Should().BeFalse();
        availability.MarkUnavailable(null!, "X").Should().BeFalse();

        availability.Keys.Should().Equal("dsp--A", "dsp--B");
    }
}

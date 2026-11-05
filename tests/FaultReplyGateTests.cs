using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class FaultReplyGateTests
{
    private const string Attr = "activeFaultList";

    [Fact]
    public void Applies_Replies_Sent_Since_The_Last_Turn_Off()
    {
        var gate = new FaultReplyGate(Attr);
        var a = gate.Tag;
        var b = gate.Tag;

        gate.ShouldApply(a).Should().BeTrue();
        gate.ShouldApply(b).Should().BeTrue();
    }

    [Fact]
    public void Drops_A_Periodic_Reply_That_Arrives_After_Polling_Is_Turned_Off()
    {
        var gate = new FaultReplyGate(Attr);
        var periodic = gate.Tag;

        gate.PollingTurnedOff();

        gate.ShouldApply(periodic).Should().BeFalse();
    }

    [Fact]
    public void A_Stale_Periodic_Reply_Does_Not_Take_A_Later_One_Shots_Place()
    {
        // Periodic poll A is outstanding when polling is turned off; one-shot B is sent before A replies.
        var gate = new FaultReplyGate(Attr);
        var a = gate.Tag;
        gate.PollingTurnedOff();
        var b = gate.Tag;

        gate.ShouldApply(a).Should().BeFalse();
        gate.ShouldApply(b).Should().BeTrue();
    }

    [Fact]
    public void Applies_Every_One_Shot_While_Off()
    {
        var gate = new FaultReplyGate(Attr);
        gate.PollingTurnedOff();
        var first = gate.Tag;
        var second = gate.Tag;

        gate.ShouldApply(first).Should().BeTrue();
        gate.ShouldApply(second).Should().BeTrue();
    }

    [Fact]
    public void Drops_A_One_Shot_Sent_Before_Polling_Is_Turned_Off()
    {
        var gate = new FaultReplyGate(Attr);
        var oneShot = gate.Tag;

        gate.PollingTurnedOff();

        gate.ShouldApply(oneShot).Should().BeFalse();
    }

    [Theory]
    [InlineData("activeFaultList#0", true)]
    [InlineData("activeFaultList#12", true)]
    [InlineData("activeFaultList", false)]
    [InlineData("level", false)]
    [InlineData(null, false)]
    public void Recognises_Its_Own_Tags(string? queued, bool expected)
    {
        new FaultReplyGate(Attr).IsFaultReply(queued).Should().Be(expected);
    }
}

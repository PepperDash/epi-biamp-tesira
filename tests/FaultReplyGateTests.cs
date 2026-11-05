using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class FaultReplyGateTests
{
    [Fact]
    public void Applies_Every_Reply_While_Polling_Is_On()
    {
        var gate = new FaultReplyGate();

        gate.PollSent(pollingEnabled: true);
        gate.PollSent(pollingEnabled: true);

        gate.ShouldApply(pollingEnabled: true).Should().BeTrue();
        gate.ShouldApply(pollingEnabled: true).Should().BeTrue();
        gate.PendingOneShots.Should().Be(0);
    }

    [Fact]
    public void Drops_A_Periodic_Reply_That_Arrives_After_Polling_Is_Turned_Off()
    {
        var gate = new FaultReplyGate();

        gate.PollSent(pollingEnabled: true);
        gate.PollingTurnedOff();

        gate.ShouldApply(pollingEnabled: false).Should().BeFalse();
    }

    [Fact]
    public void Applies_The_Reply_To_A_One_Shot_Poll_While_Off()
    {
        var gate = new FaultReplyGate();

        gate.PollSent(pollingEnabled: false);

        gate.ShouldApply(pollingEnabled: false).Should().BeTrue();
        gate.ShouldApply(pollingEnabled: false).Should().BeFalse();
    }

    [Fact]
    public void Applies_Both_Replies_When_Two_One_Shots_Are_Outstanding()
    {
        var gate = new FaultReplyGate();

        gate.PollSent(pollingEnabled: false);
        gate.PollSent(pollingEnabled: false);

        gate.ShouldApply(pollingEnabled: false).Should().BeTrue();
        gate.ShouldApply(pollingEnabled: false).Should().BeTrue();
        gate.ShouldApply(pollingEnabled: false).Should().BeFalse();
    }

    [Fact]
    public void Turning_Polling_Off_Drops_Outstanding_One_Shots()
    {
        var gate = new FaultReplyGate();

        gate.PollSent(pollingEnabled: false);
        gate.PollingTurnedOff();

        gate.ShouldApply(pollingEnabled: false).Should().BeFalse();
    }
}

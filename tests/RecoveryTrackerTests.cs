using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class RecoveryTrackerTests
{
    [Fact]
    public void Keeps_A_Control_Pending_Until_It_Is_Completed()
    {
        // A stray successful reply clears availability, not recovery: only Complete does.
        var recovery = new RecoveryTracker();
        recovery.MarkNeeded("dsp--Fader1", fullSetup: true);

        recovery.IsPending("dsp--Fader1").Should().BeTrue();
        recovery.Complete("dsp--Fader1").Should().BeTrue();
        recovery.IsPending("dsp--Fader1").Should().BeFalse();
        recovery.Complete("dsp--Fader1").Should().BeFalse();
    }

    [Fact]
    public void Full_Setup_Once_Asked_For_Stays_Asked_For()
    {
        // Unavailable during startup (full setup), then again later (subscriptions only).
        var recovery = new RecoveryTracker();
        recovery.MarkNeeded("dsp--Dialer1", fullSetup: true);
        recovery.MarkNeeded("dsp--Dialer1", fullSetup: false);

        recovery.NeedsFullSetup("dsp--Dialer1").Should().BeTrue();
    }

    [Fact]
    public void A_Runtime_Outage_Needs_Subscriptions_Only()
    {
        var recovery = new RecoveryTracker();
        recovery.MarkNeeded("dsp--Fader1", fullSetup: false);

        recovery.NeedsFullSetup("dsp--Fader1").Should().BeFalse();
    }

    [Fact]
    public void Full_Setup_Is_Forgotten_Once_Restored()
    {
        var recovery = new RecoveryTracker();
        recovery.MarkNeeded("dsp--Dialer1", fullSetup: true);
        recovery.Complete("dsp--Dialer1");

        recovery.MarkNeeded("dsp--Dialer1", fullSetup: false);

        recovery.NeedsFullSetup("dsp--Dialer1").Should().BeFalse();
    }

    [Fact]
    public void Lists_Keys_Sorted_And_Ignores_Empty_Keys()
    {
        var recovery = new RecoveryTracker();
        recovery.MarkNeeded("dsp--B", false);
        recovery.MarkNeeded("dsp--A", false);
        recovery.MarkNeeded("", true);
        recovery.MarkNeeded(null!, true);

        recovery.Keys.Should().Equal("dsp--A", "dsp--B");
        recovery.Count.Should().Be(2);
    }
}

public class RecoveryTrackerRestoreTests
{
    [Fact]
    public void Is_Restored_Only_After_A_Restore_Was_Sent()
    {
        // A subscribed flag left over from before the outage must not count.
        var recovery = new RecoveryTracker();
        recovery.MarkNeeded("dsp--Fader1", fullSetup: false);

        recovery.IsRestored("dsp--Fader1").Should().BeFalse();
        recovery.MarkRestoreSent("dsp--Fader1");
        recovery.IsRestored("dsp--Fader1").Should().BeTrue();
    }

    [Fact]
    public void An_Unavailable_Reply_After_The_Restore_Undoes_It()
    {
        var recovery = new RecoveryTracker();
        recovery.MarkNeeded("dsp--Fader1", fullSetup: false);
        recovery.MarkRestoreSent("dsp--Fader1");

        recovery.MarkNeeded("dsp--Fader1", fullSetup: false);

        recovery.IsRestored("dsp--Fader1").Should().BeFalse();
        recovery.IsPending("dsp--Fader1").Should().BeTrue();
    }

    [Fact]
    public void Restore_Sent_For_An_Unknown_Key_Is_Ignored()
    {
        var recovery = new RecoveryTracker();
        recovery.MarkRestoreSent("dsp--Fader1");

        recovery.IsPending("dsp--Fader1").Should().BeFalse();
        recovery.IsRestored("dsp--Fader1").Should().BeFalse();
    }
}

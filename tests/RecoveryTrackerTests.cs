using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class RecoveryTrackerTests
{
    [Fact]
    public void A_Failed_Setup_Write_Is_Not_Undone_By_A_Later_Subscribe_Reply()
    {
        // A full restore's setup writes run in the foreground lane and its subscribes in the
        // background, so a failed write can be answered before the subscribe's +OK. The failure
        // must hold until the next restore is sent.
        var recovery = new RecoveryTracker();
        recovery.MarkNeeded("dsp--Dialer1", fullSetup: true);
        recovery.MarkRestoreSent("dsp--Dialer1");
        recovery.ExpectSubscribe("dsp--Dialer1");

        recovery.Fail("dsp--Dialer1");                               // set redialEnable -> -GENERAL_FAILURE
        recovery.Acknowledge("dsp--Dialer1", isSubscribe: true);     // subscribe callState -> +OK

        recovery.IsRestored("dsp--Dialer1").Should().BeFalse();
        recovery.IsPending("dsp--Dialer1").Should().BeTrue();
        recovery.NeedsFullSetup("dsp--Dialer1").Should().BeTrue();   // the retry re-sends the setup

        recovery.MarkRestoreSent("dsp--Dialer1");                    // next retry, writes succeed
        recovery.Acknowledge("dsp--Dialer1", isSubscribe: true);
        recovery.IsRestored("dsp--Dialer1").Should().BeTrue();
    }

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
    private static RecoveryTracker Pending(string key)
    {
        var recovery = new RecoveryTracker();
        recovery.MarkNeeded(key, fullSetup: false);
        return recovery;
    }

    [Fact]
    public void A_Restore_Counts_Only_Once_Acknowledged()
    {
        // Neither a leftover subscribed flag nor a restore merely sent ends recovery.
        var recovery = Pending("dsp--Fader1");

        recovery.IsRestored("dsp--Fader1").Should().BeFalse();
        recovery.MarkRestoreSent("dsp--Fader1");
        recovery.IsRestored("dsp--Fader1").Should().BeFalse();
        recovery.Acknowledge("dsp--Fader1", isSubscribe: true);
        recovery.IsRestored("dsp--Fader1").Should().BeTrue();
    }

    [Fact]
    public void A_Success_Before_Any_Restore_Was_Sent_Does_Not_Count()
    {
        // A successful reply to a command from before the restore (sent while unavailable).
        var recovery = Pending("dsp--Fader1");

        recovery.Acknowledge("dsp--Fader1", isSubscribe: true);
        recovery.MarkRestoreSent("dsp--Fader1");

        recovery.IsRestored("dsp--Fader1").Should().BeFalse();
    }

    [Fact]
    public void A_Failed_Restore_Command_Undoes_It_Even_After_A_Success()
    {
        // -GENERAL_FAILURE on the subscribe after the read was answered.
        var recovery = Pending("dsp--Fader1");
        recovery.MarkRestoreSent("dsp--Fader1");
        recovery.Acknowledge("dsp--Fader1", isSubscribe: true);

        recovery.Fail("dsp--Fader1");

        recovery.IsRestored("dsp--Fader1").Should().BeFalse();
        recovery.IsPending("dsp--Fader1").Should().BeTrue();
    }

    [Fact]
    public void An_Unavailable_Reply_After_The_Restore_Undoes_It()
    {
        var recovery = Pending("dsp--Fader1");
        recovery.MarkRestoreSent("dsp--Fader1");
        recovery.Acknowledge("dsp--Fader1", isSubscribe: true);

        recovery.MarkNeeded("dsp--Fader1", fullSetup: false);

        recovery.IsRestored("dsp--Fader1").Should().BeFalse();
    }

    [Fact]
    public void A_Rejected_Or_Discarded_Restore_Stays_Pending()
    {
        // No reply ever comes: sent, never acknowledged.
        var recovery = Pending("dsp--Fader1");
        recovery.MarkRestoreSent("dsp--Fader1");

        recovery.IsRestored("dsp--Fader1").Should().BeFalse();
    }

    [Fact]
    public void Unknown_Keys_Are_Ignored()
    {
        var recovery = new RecoveryTracker();
        recovery.MarkRestoreSent("dsp--Fader1");
        recovery.Acknowledge("dsp--Fader1", isSubscribe: true);
        recovery.Fail("dsp--Fader1");

        recovery.IsPending("dsp--Fader1").Should().BeFalse();
        recovery.IsRestored("dsp--Fader1").Should().BeFalse();
    }

    [Fact]
    public void Once_A_Subscribe_Is_Queued_Only_A_Subscribe_Reply_Acknowledges()
    {
        // A fader's range read answered while its subscribe was dropped must not end recovery.
        var recovery = Pending("dsp--Fader1");
        recovery.MarkRestoreSent("dsp--Fader1");
        recovery.ExpectSubscribe("dsp--Fader1");

        recovery.Acknowledge("dsp--Fader1", isSubscribe: false);
        recovery.IsRestored("dsp--Fader1").Should().BeFalse();

        recovery.Acknowledge("dsp--Fader1", isSubscribe: true);
        recovery.IsRestored("dsp--Fader1").Should().BeTrue();
    }

    [Fact]
    public void A_Read_Answered_Before_The_Subscribe_Was_Queued_No_Longer_Counts()
    {
        var recovery = Pending("dsp--Fader1");
        recovery.MarkRestoreSent("dsp--Fader1");
        recovery.Acknowledge("dsp--Fader1", isSubscribe: false);

        recovery.ExpectSubscribe("dsp--Fader1");

        recovery.IsRestored("dsp--Fader1").Should().BeFalse();
    }

    [Fact]
    public void A_Polled_Control_Is_Acknowledged_By_Its_State_Read()
    {
        // A crosspoint or polled router queues no subscribe.
        var recovery = Pending("dsp--Xpt1");
        recovery.MarkRestoreSent("dsp--Xpt1");

        recovery.Acknowledge("dsp--Xpt1", isSubscribe: false);

        recovery.IsRestored("dsp--Xpt1").Should().BeTrue();
    }

    [Fact]
    public void A_Subscribe_Expected_Before_Any_Restore_Is_Ignored()
    {
        var recovery = Pending("dsp--Fader1");
        recovery.ExpectSubscribe("dsp--Fader1");
        recovery.MarkRestoreSent("dsp--Fader1");

        recovery.Acknowledge("dsp--Fader1", isSubscribe: false);

        recovery.IsRestored("dsp--Fader1").Should().BeTrue();
    }
}

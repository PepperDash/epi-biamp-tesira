using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class ProbeRepliesTests
{
    [Theory]
    [InlineData("-ERR ALREADY_SUBSCRIBED", ProbeReply.Healthy)]
    [InlineData("+OK", ProbeReply.Lost)]
    [InlineData("+OK ", ProbeReply.Lost)]
    [InlineData("-ERR address not found: [0 0 0]", ProbeReply.ConfigError)]
    [InlineData("-ERR 'bogusAttr' is not supported by LevelControlInterface::Attributes", ProbeReply.ConfigError)]
    [InlineData("-ERR operation subscribe not supported on attribute: minLevel", ProbeReply.ConfigError)]
    [InlineData("-ERR INVALID_PARAMETER Index out of range:channelIndex min:1 max:1 received:99", ProbeReply.ConfigError)]
    [InlineData("-ERR SOMETHING_ELSE", ProbeReply.OtherError)]
    [InlineData("", ProbeReply.OtherError)]
    [InlineData(null, ProbeReply.OtherError)]
    public void Classify_Matches_Hardware_Replies(string? response, ProbeReply expected)
    {
        ProbeReplies.Classify(response!).Should().Be(expected);
    }

    [Theory]
    [InlineData("\"RMA-ROOM\" subscribe level 1 RMA-ROOM__level1 500", true)]
    [InlineData("\"RMA-ROOM\" unsubscribe level 1 RMA-ROOM__level1", false)]
    [InlineData("\"RMA-ROOM\" get level 1", false)]
    [InlineData(null, false)]
    public void IsSubscribeCommand(string? command, bool expected)
    {
        ProbeReplies.IsSubscribeCommand(command!).Should().Be(expected);
    }
}

public class WatchdogPatrolTests
{
    private static readonly List<string> Six = new() { "a", "b", "c", "d", "e", "f" };

    [Fact]
    public void Default_Sentinel_Is_The_First_Candidate_And_The_Patrol_Rotates()
    {
        var patrol = new WatchdogPatrol<string>(2);

        patrol.NextProbeSet(Six, null).Should().Equal("a", "b", "c");
        patrol.NextProbeSet(Six, null).Should().Equal("a", "d", "e");
        patrol.NextProbeSet(Six, null).Should().Equal("a", "f", "b"); // wraps
    }

    [Fact]
    public void Configured_Sentinels_Are_Probed_Every_Tick_And_Left_Out_Of_The_Patrol()
    {
        var patrol = new WatchdogPatrol<string>(1);

        patrol.NextProbeSet(Six, new[] { "c", "f" }).Should().Equal("c", "f", "a");
        patrol.NextProbeSet(Six, new[] { "c", "f" }).Should().Equal("c", "f", "b");
        patrol.NextProbeSet(Six, new[] { "c", "f" }).Should().Equal("c", "f", "d");
    }

    [Fact]
    public void Sentinels_That_Are_Not_Candidates_Fall_Back_To_The_First_Candidate()
    {
        var patrol = new WatchdogPatrol<string>(0);
        patrol.NextProbeSet(Six, new[] { "zzz" }).Should().Equal("a");
    }

    [Fact]
    public void Quarantined_Components_Are_Never_Probed()
    {
        var patrol = new WatchdogPatrol<string>(10);
        patrol.Quarantine("a").Should().BeTrue();
        patrol.Quarantine("a").Should().BeFalse();
        patrol.Quarantine("d");

        var probe = patrol.NextProbeSet(Six, new[] { "a" });
        probe.Should().NotContain(new[] { "a", "d" });
        probe.First().Should().Be("b"); // the sentinel is quarantined, so the first usable candidate stands in
        patrol.QuarantinedCount.Should().Be(2);

        patrol.ClearQuarantine();
        patrol.IsQuarantined("a").Should().BeFalse();
    }

    [Fact]
    public void No_Candidates_Means_Nothing_To_Probe()
    {
        new WatchdogPatrol<string>(2).NextProbeSet(new List<string>(), new[] { "a" }).Should().BeEmpty();
    }

    [Fact]
    public void Patrol_Smaller_Than_Size_Takes_What_There_Is()
    {
        new WatchdogPatrol<string>(5).NextProbeSet(new List<string> { "a", "b" }, null).Should().Equal("a", "b");
    }
}

public class ProbeBatchTests
{
    // Commands are distinct objects, as QueuedCommand instances are.
    private sealed class Cmd { public string Name = ""; public override string ToString() => Name; }
    private static Cmd C(string name) => new Cmd { Name = name };

    [Fact]
    public void Closes_When_Every_Expected_Reply_Is_Healthy()
    {
        var batch = new ProbeBatch<string, Cmd>();
        var level = C("fader level"); var mute = C("fader mute"); var state = C("state");
        batch.Begin(new[] { "fader", "state" });
        batch.ExpectReply("fader", level);
        batch.ExpectReply("fader", mute);
        batch.ExpectReply("state", state);
        batch.EndCapture().Should().BeTrue();

        batch.Record(level, "-ERR ALREADY_SUBSCRIBED").Should().Be(ProbeReply.Healthy);
        batch.Record(state, "-ERR ALREADY_SUBSCRIBED").Should().Be(ProbeReply.Healthy);
        batch.IsOpen.Should().BeTrue();
        batch.Record(mute, "-ERR ALREADY_SUBSCRIBED").Should().Be(ProbeReply.Healthy);
        batch.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void A_Lost_Reply_Closes_The_Batch_At_Once()
    {
        var batch = new ProbeBatch<string, Cmd>();
        var a = C("a"); var b = C("b");
        batch.Begin(new[] { "a", "b" });
        batch.ExpectReply("a", a);
        batch.ExpectReply("b", b);
        batch.EndCapture();

        batch.Record(a, "+OK").Should().Be(ProbeReply.Lost);
        batch.IsOpen.Should().BeFalse();
        batch.Record(b, "-ERR ALREADY_SUBSCRIBED").Should().BeNull();
    }

    [Fact]
    public void A_Subscribe_Queued_Before_The_Probe_Does_Not_Take_The_Probes_Place()
    {
        var batch = new ProbeBatch<string, Cmd>();
        var earlier = C("a, queued before the probe");
        var probe = C("a probe");
        batch.Begin(new[] { "a" });
        batch.ExpectReply("a", probe);
        batch.EndCapture();

        batch.Record(earlier, "-ERR ALREADY_SUBSCRIBED").Should().BeNull();
        batch.IsOpen.Should().BeTrue();
        batch.Record(probe, "+OK").Should().Be(ProbeReply.Lost);
    }

    [Fact]
    public void Commands_From_Non_Members_Are_Not_Expected()
    {
        var batch = new ProbeBatch<string, Cmd>();
        var a = C("a"); var other = C("other");
        batch.Begin(new[] { "a" });
        batch.ExpectReply("a", a);
        batch.ExpectReply("other", other);
        batch.EndCapture();

        batch.Record(other, "+OK").Should().BeNull();
        batch.IsOpen.Should().BeTrue();
        batch.Record(a, "-ERR address not found: [0 0 0]").Should().Be(ProbeReply.ConfigError);
        batch.IsOpen.Should().BeFalse();
        batch.Record(a, "+OK").Should().BeNull();
    }

    [Fact]
    public void Commands_Queued_After_Capture_Ends_Are_Not_Expected()
    {
        var batch = new ProbeBatch<string, Cmd>();
        batch.Begin(new[] { "a" });
        batch.ExpectReply("a", C("probe"));
        batch.EndCapture();
        batch.ExpectReply("a", C("user subscribe after the probe"));

        batch.Outstanding.Should().Be(1);
    }

    [Fact]
    public void A_Batch_Where_Nothing_Was_Queued_Closes_At_End_Of_Capture()
    {
        var batch = new ProbeBatch<string, Cmd>();
        batch.Begin(new[] { "meter" }); // opted out between pick and probe
        batch.EndCapture().Should().BeFalse();
        batch.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Reply_Arriving_During_Capture_Does_Not_Close_Early()
    {
        // The queue can send and answer the first probe while later members are still queuing.
        var batch = new ProbeBatch<string, Cmd>();
        var a = C("a"); var b = C("b");
        batch.Begin(new[] { "a", "b" });
        batch.ExpectReply("a", a);
        batch.Record(a, "-ERR ALREADY_SUBSCRIBED");
        batch.IsOpen.Should().BeTrue();
        batch.ExpectReply("b", b);
        batch.EndCapture().Should().BeTrue();
        batch.Record(b, "-ERR ALREADY_SUBSCRIBED");
        batch.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Reports_Members_Whose_Probe_Queued_Nothing()
    {
        var batch = new ProbeBatch<string, Cmd>();
        batch.Begin(new[] { "fader", "routerSwitcher" });
        batch.ExpectReply("fader", C("fader level"));
        // the router-type switcher's Subscribe() queues nothing

        batch.MembersWithoutExpectedReplies().Should().Equal("routerSwitcher");
    }

    [Fact]
    public void A_Probe_Rejected_By_A_Full_Queue_Is_Not_Reported_As_Silent()
    {
        var batch = new ProbeBatch<string, Cmd>();
        batch.Begin(new[] { "router", "routerSwitcher" });
        batch.MarkAttempted("router"); // its subscribe was rejected: queue full

        batch.MembersWithoutExpectedReplies().Should().Equal("routerSwitcher");
        batch.Outstanding.Should().Be(0); // nothing to wait for
    }

    [Fact]
    public void Contains_Is_False_Once_Closed()
    {
        var batch = new ProbeBatch<string, Cmd>();
        batch.Begin(new[] { "a" });
        batch.Contains("a").Should().BeTrue();
        batch.Close();
        batch.Contains("a").Should().BeFalse();
    }
}

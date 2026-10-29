using FluentAssertions;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class CommandLanesTests
{
    [Theory]
    [InlineData("\"RMA-ROOM\" subscribe level 1 RMA-ROOM__level1 500", true)]
    [InlineData("\"RMA-ROOM\" unsubscribe level 1 RMA-ROOM__level1", true)]
    [InlineData("Router6 subscribe input 1 r 250", true)]
    [InlineData("\"Room 1 Level\" subscribe level 1 x 500", true)] // quoted tag with spaces
    [InlineData("SESSION set verbose false", false)] // must stay ahead of the reads queued after it
    [InlineData("SESSION get aliases", false)]
    [InlineData("\"RMA-ROOM\" set level 1 -10.0", false)]
    [InlineData("\"RMA-ROOM\" increment level 1 2.0", false)]
    [InlineData("\"RMA-ROOM\" toggle mute 1", false)]
    [InlineData("\"RMA-ROOM\" get level 1", false)]            // read-backs follow their set
    [InlineData("\"Router6\" set input 2 3", false)]
    [InlineData("DEVICE recallPresetByName \"RmA-SYSON\"", false)]
    [InlineData("DEVICE savePreset 1013", false)]
    [InlineData("\"Dialer1\" dial 1 1 5551212", false)]
    [InlineData("\"Subscribe Room\" set level 1 0", false)]    // the word in the tag doesn't count
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("garbage", false)]
    [InlineData("\"unterminated subscribe", false)]
    public void IsBackground_Puts_Only_Subscriptions_In_The_Background(string? command, bool expected)
    {
        CommandLanes.IsBackground(command!).Should().Be(expected);
    }
}

public class CommandLanesReadTests
{
    [Theory]
    [InlineData("\"RMA-ROOM\" get minLevel 1", true)]
    [InlineData("\"RMA-ROOM\" subscribe level 1 x 250", true)]
    [InlineData("\"RMA-ROOM\" unsubscribe level 1 x", true)]
    [InlineData("\"Dialer1\" set redialEnable 1 true", false)]   // configuration write
    [InlineData("\"Dialer1\" set autoAnswerRingCount 1 2", false)]
    [InlineData("\"RMA-ROOM\" toggle mute 1", false)]
    [InlineData("SESSION set verbose false", false)]
    [InlineData("SESSION get aliases", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsReadOrSubscription_Excludes_Writes(string? command, bool expected)
    {
        CommandLanes.IsReadOrSubscription(command!).Should().Be(expected);
    }
}

public class TwoLaneQueueTests
{
    [Fact]
    public void Foreground_Items_Come_Out_First_Each_Lane_In_Order()
    {
        var q = new TwoLaneQueue<string>(100);
        q.Enqueue("sub1", true);
        q.Enqueue("sub2", true);
        q.Enqueue("set1", false);
        q.Enqueue("sub3", true);
        q.Enqueue("set2", false);

        var order = new System.Collections.Generic.List<string>();
        while (q.TryDequeue(out var item)) order.Add(item);

        order.Should().Equal("set1", "set2", "sub1", "sub2", "sub3");
    }

    [Fact]
    public void A_Foreground_Item_Queued_Mid_Burst_Goes_Next()
    {
        var q = new TwoLaneQueue<string>(100);
        for (var i = 0; i < 16; i++) q.Enqueue("restore" + i, true);
        q.TryDequeue(out var first).Should().BeTrue();
        first.Should().Be("restore0");

        q.Enqueue("user on", false);
        q.TryDequeue(out var next);
        next.Should().Be("user on");
    }

    [Fact]
    public void A_Steady_Foreground_Stream_Still_Lets_Background_Items_Through()
    {
        var q = new TwoLaneQueue<string>(100);
        q.Enqueue("probe1", true);
        q.Enqueue("probe2", true);
        for (var i = 0; i < 20; i++) q.Enqueue("ramp" + i, false);

        var order = new System.Collections.Generic.List<string>();
        while (q.TryDequeue(out var item)) order.Add(item);

        order.IndexOf("probe1").Should().Be(TwoLaneQueue<string>.MaxForegroundRun);
        order.IndexOf("probe2").Should().Be(2 * TwoLaneQueue<string>.MaxForegroundRun + 1);
        order.Should().HaveCount(22);
    }

    [Fact]
    public void A_Foreground_Run_With_Nothing_Waiting_Does_Not_Count()
    {
        var q = new TwoLaneQueue<string>(100);
        for (var i = 0; i < 20; i++) q.Enqueue("fg" + i, false);
        for (var i = 0; i < 20; i++) q.TryDequeue(out _);

        // A restore burst arriving after a long run of presses still waits behind the next press.
        q.Enqueue("restore", true);
        q.Enqueue("press", false);
        q.TryDequeue(out var first);
        first.Should().Be("press");
    }

    [Fact]
    public void Capacity_Counts_Both_Lanes()
    {
        var q = new TwoLaneQueue<string>(2);
        q.Enqueue("a", true).Should().BeTrue();
        q.Enqueue("b", false).Should().BeTrue();
        q.Enqueue("c", false).Should().BeFalse();
        q.Count.Should().Be(2);
    }

    [Fact]
    public void Clear_Empties_Both_Lanes()
    {
        var q = new TwoLaneQueue<string>(10);
        q.Enqueue("a", true);
        q.Enqueue("b", false);
        q.Clear();
        q.IsEmpty.Should().BeTrue();
        q.TryDequeue(out _).Should().BeFalse();
    }
}

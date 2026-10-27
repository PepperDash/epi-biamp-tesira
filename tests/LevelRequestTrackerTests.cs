using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class LevelRequestTrackerTests
{
    private const bool Step = false;
    private const bool Set = true;

    [Fact]
    public void Corrects_When_Nothing_Else_Is_Queued()
    {
        var tracker = new LevelRequestTracker();
        tracker.RecordRequest(Step);

        tracker.RecordResponse(); // the rejected step

        tracker.ShouldCorrect().Should().BeTrue();
    }

    [Fact]
    public void Corrects_When_Only_Steps_Are_Queued_Behind_The_Rejected_Step()
    {
        var tracker = new LevelRequestTracker();
        tracker.RecordRequest(Step);
        tracker.RecordRequest(Step);

        tracker.RecordResponse();

        tracker.ShouldCorrect().Should().BeTrue();
    }

    [Fact]
    public void Skips_When_A_Set_Is_Queued_Behind_The_Rejected_Step()
    {
        var tracker = new LevelRequestTracker();
        tracker.RecordRequest(Step);
        tracker.RecordRequest(Set);

        tracker.RecordResponse();

        tracker.ShouldCorrect().Should().BeFalse("the correction would land after the SET and undo it");
    }

    [Fact]
    public void Skips_For_Step_Set_Step()
    {
        // UP from 11 dB (max 12), then SET 6, then DOWN 2, all queued before the error arrives.
        var tracker = new LevelRequestTracker();
        tracker.RecordRequest(Step);
        tracker.RecordRequest(Set);
        tracker.RecordRequest(Step);

        tracker.RecordResponse();

        tracker.ShouldCorrect().Should().BeFalse("a later step must not clear the protection for the queued SET");
    }

    [Fact]
    public void Corrects_Once_The_Queued_Set_Has_Been_Answered()
    {
        var tracker = new LevelRequestTracker();
        tracker.RecordRequest(Set);
        tracker.RecordRequest(Step);

        tracker.RecordResponse(); // SET +OK
        tracker.RecordResponse(); // step rejected

        tracker.ShouldCorrect().Should().BeTrue();
    }

    [Fact]
    public void Reset_Clears_Pending_Requests()
    {
        var tracker = new LevelRequestTracker();
        tracker.RecordRequest(Step);
        tracker.RecordRequest(Set);

        tracker.Reset();

        tracker.ShouldCorrect().Should().BeTrue();
    }

    [Fact]
    public void Pending_Requests_Are_Bounded()
    {
        var tracker = new LevelRequestTracker();
        tracker.RecordRequest(Set);
        for (var i = 0; i < LevelRequestTracker.MaxPending; i++) tracker.RecordRequest(Step);

        tracker.ShouldCorrect().Should().BeTrue("the oldest entry is dropped once the bound is reached");
    }
}

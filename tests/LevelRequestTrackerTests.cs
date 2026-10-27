using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class LevelRequestTrackerTests
{
    [Fact]
    public void Corrects_When_No_Request_Was_Made()
    {
        new LevelRequestTracker().ShouldCorrect(13).Should().BeTrue();
    }

    [Fact]
    public void Corrects_When_The_Rejected_Step_Is_The_Latest_Request()
    {
        var tracker = new LevelRequestTracker();
        tracker.RecordSet(6);
        tracker.RecordStep();

        tracker.ShouldCorrect(13).Should().BeTrue();
    }

    [Fact]
    public void Skips_When_A_Newer_Set_Is_Queued_Behind_The_Rejected_Step()
    {
        var tracker = new LevelRequestTracker();
        tracker.RecordStep();
        tracker.RecordSet(6);

        tracker.ShouldCorrect(13).Should().BeFalse("the correction would land after SET 6 and undo it");
    }

    [Fact]
    public void Corrects_When_The_Rejected_Request_Is_The_Latest_Set()
    {
        var tracker = new LevelRequestTracker();
        tracker.RecordStep();
        tracker.RecordSet(14);

        tracker.ShouldCorrect(14.0000004).Should().BeTrue();
    }
}

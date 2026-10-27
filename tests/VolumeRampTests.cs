using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class VolumeRampTests
{
    private readonly List<bool> steps = new();
    private int upPresses;

    // Long intervals so real timers never fire during a test; timer callbacks are driven directly.
    private VolumeRamp CreateRamp(int holdTimeoutMs = 60_000) =>
        new(isUp => { lock (steps) steps.Add(isUp); }, () => 60_000, () => holdTimeoutMs,
            onUpPress: () => upPresses++);

    [Fact]
    public void Press_Sends_One_Step_And_Arms_Hold_Timer()
    {
        using var ramp = CreateRamp();

        ramp.Press(true);

        steps.Should().Equal(true);
        ramp.IsHeld(true).Should().BeTrue();
        ramp.IsHoldTimerArmed(true).Should().BeTrue();
        upPresses.Should().Be(1);
    }

    [Fact]
    public void Delay_Then_Repeat_Steps_While_Held_Without_New_Up_Press()
    {
        using var ramp = CreateRamp();
        ramp.Press(true);

        ramp.OnDelayElapsed(true);
        ramp.OnRepeatElapsed(true);
        ramp.OnRepeatElapsed(true);

        steps.Should().Equal(true, true, true, true);
        upPresses.Should().Be(1, "repeats are not new presses");
    }

    [Fact]
    public void Callbacks_Queued_Before_Release_Do_Nothing_After_Release()
    {
        using var ramp = CreateRamp();
        ramp.Press(true);
        ramp.OnDelayElapsed(true);
        ramp.Release(true);

        // A repeat or delay callback that was already queued on the thread pool runs late.
        ramp.OnRepeatElapsed(true);
        ramp.OnDelayElapsed(true);

        steps.Should().Equal(true, true);
        ramp.IsHeld(true).Should().BeFalse();
        ramp.IsRepeating(true).Should().BeFalse();
        ramp.IsHoldTimerArmed(true).Should().BeFalse("a late callback must not start a new held ramp");
        upPresses.Should().Be(1);
    }

    [Fact]
    public void Releasing_One_Direction_Keeps_The_Other_Hold_Timeout_Armed()
    {
        using var ramp = CreateRamp();
        ramp.Press(true);
        ramp.Press(false);

        ramp.Release(false);

        ramp.IsHoldTimerArmed(true).Should().BeTrue();
        ramp.IsHoldTimerArmed(false).Should().BeFalse();
    }

    [Fact]
    public void Hold_Timeout_Releases_Only_Its_Direction()
    {
        using var ramp = CreateRamp();
        ramp.Press(true);
        ramp.Press(false);
        ramp.OnDelayElapsed(true);
        ramp.OnDelayElapsed(false);

        ramp.FireHoldTimer(true);

        ramp.IsHeld(true).Should().BeFalse();
        ramp.IsRepeating(true).Should().BeFalse();
        ramp.IsHeld(false).Should().BeTrue();
        ramp.IsRepeating(false).Should().BeTrue();
    }

    [Fact]
    public void Stale_Hold_Callback_Does_Not_Release_A_Newer_Press()
    {
        using var ramp = CreateRamp();
        ramp.Press(true);
        ramp.Release(true);
        ramp.Press(true);

        // The first press's hold callback runs late, after the new press re-armed the timer.
        ramp.OnHoldElapsed(true);

        ramp.IsHeld(true).Should().BeTrue();
        ramp.IsHoldTimerArmed(true).Should().BeTrue();
    }

    [Fact]
    public void Cancel_Stops_Repeats_In_Both_Directions_But_Keeps_Hold_Timers()
    {
        using var ramp = CreateRamp();
        ramp.Press(true);
        ramp.OnDelayElapsed(true);

        ramp.Cancel();
        ramp.OnRepeatElapsed(true);

        steps.Should().Equal(true, true);
        ramp.IsRepeating(true).Should().BeFalse();
        ramp.IsHoldTimerArmed(true).Should().BeTrue();
    }

    [Fact]
    public void Zero_Hold_Timeout_Disables_The_Safety_Stop()
    {
        using var ramp = CreateRamp(holdTimeoutMs: 0);

        ramp.Press(false);

        ramp.IsHoldTimerArmed(false).Should().BeFalse();
    }

    [Fact]
    public void Real_Timers_Repeat_Then_Stop_On_Release()
    {
        using var ramp = new VolumeRamp(isUp => { lock (steps) steps.Add(isUp); }, () => 20, () => 60_000);

        ramp.Press(false);
        Thread.Sleep(VolumeRamp.RepeatDelayMs + 150);
        ramp.Release(false);
        int countAtRelease;
        lock (steps) countAtRelease = steps.Count;
        Thread.Sleep(150);

        countAtRelease.Should().BeGreaterThan(2, "the ramp repeats while held");
        lock (steps) steps.Count.Should().BeInRange(countAtRelease, countAtRelease + 1,
            "at most one in-flight step may land after release");
        steps.Should().AllSatisfy(s => s.Should().BeFalse());
    }
}

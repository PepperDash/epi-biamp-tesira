using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class PresetHoldButtonTests
{
    private readonly List<string> actions = new();

    // Long hold time so the real timer never fires; the hold is driven through the test hooks.
    private PresetHoldButton Create() =>
        new(60_000, () => { lock (actions) actions.Add("recall"); }, () => { lock (actions) actions.Add("save"); });

    [Fact]
    public void Short_Press_Recalls_On_Release()
    {
        using var button = Create();
        button.Set(true);
        actions.Should().BeEmpty("nothing happens until release or hold");

        button.Set(false);

        actions.Should().Equal("recall");
    }

    [Fact]
    public void Hold_Saves_And_The_Release_Does_Nothing()
    {
        using var button = Create();
        button.Set(true);

        button.FireHold();
        button.Set(false);

        actions.Should().Equal("save");
    }

    [Fact]
    public void Hold_Timer_Firing_After_Release_Does_Not_Save()
    {
        using var button = Create();
        button.Set(true);
        button.Set(false);

        // The timer callback was already queued when the release landed.
        button.OnHoldElapsed();

        actions.Should().Equal("recall");
    }

    [Fact]
    public void Stale_Hold_Callback_From_An_Earlier_Press_Is_Ignored()
    {
        using var button = Create();
        button.Set(true);
        button.Set(false);
        button.Set(true); // re-arms the hold timer

        button.OnHoldElapsed(); // the first press's callback, running late

        actions.Should().Equal("recall");
        button.IsHoldTimerArmed.Should().BeTrue();
    }

    [Fact]
    public void Cancel_Drops_A_Pending_Press_Without_Recall_Or_Save()
    {
        using var button = Create();
        button.Set(true);

        button.Cancel();     // bridge went offline; the release never arrives
        button.OnHoldElapsed();
        button.Set(false);

        actions.Should().BeEmpty();
        button.IsHoldTimerArmed.Should().BeFalse();
    }

    [Fact]
    public void Release_Without_Press_Does_Nothing()
    {
        using var button = Create();

        button.Set(false);

        actions.Should().BeEmpty();
    }

    [Fact]
    public void Each_Press_Gets_Exactly_One_Action()
    {
        using var button = Create();
        button.Set(true);
        button.Set(false);
        button.Set(true);
        button.FireHold();
        button.Set(false);
        button.Set(true);
        button.Set(false);

        actions.Should().Equal("recall", "save", "recall");
    }

    [Fact]
    public void Real_Timer_Saves_After_The_Hold_Time()
    {
        using var button = new PresetHoldButton(50, () => { lock (actions) actions.Add("recall"); },
            () => { lock (actions) actions.Add("save"); });

        button.Set(true);
        Thread.Sleep(250);
        button.Set(false);

        lock (actions) actions.Should().Equal("save");
    }
}

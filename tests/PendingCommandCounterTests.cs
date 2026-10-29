using FluentAssertions;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class PendingCommandCounterTests
{
    private readonly object fader = new();
    private readonly object other = new();

    [Fact]
    public void Tracks_Each_Key_Separately()
    {
        var counter = new PendingCommandCounter();
        counter.Add(fader);

        counter.Has(fader).Should().BeTrue();
        counter.Has(other).Should().BeFalse();
    }

    [Fact]
    public void Stays_Pending_Until_Every_Add_Is_Removed()
    {
        var counter = new PendingCommandCounter();
        counter.Add(fader);
        counter.Add(fader);

        counter.Remove(fader);
        counter.Has(fader).Should().BeTrue();

        counter.Remove(fader);
        counter.Has(fader).Should().BeFalse();
    }

    [Fact]
    public void Remove_Without_Add_Is_Ignored()
    {
        var counter = new PendingCommandCounter();
        counter.Remove(fader);
        counter.Add(fader);

        counter.Has(fader).Should().BeTrue();
    }

    [Fact]
    public void Clear_Drops_Everything()
    {
        var counter = new PendingCommandCounter();
        counter.Add(fader);
        counter.Add(other);

        counter.Clear();

        counter.Has(fader).Should().BeFalse();
        counter.Has(other).Should().BeFalse();
    }
}

public class PendingCommandCounterChangeVerbTests
{
    [Theory]
    [InlineData("\"Main Level\" set level 1 -10.000000", true)]
    [InlineData("\"Room set Main\" set levelOut 2 -6.000000", true)]
    [InlineData("\"Room set Main\" increment levelOut 2 1", true)]
    [InlineData("\"Room set Main\" get level 1", false)]
    [InlineData("\"Main Level\" decrement level 1 1", true)]
    [InlineData("\"Room set Main\" subscribe levelOut 2 \"x\" 500", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Matches_Only_The_Verb_After_The_Instance_Tag(string? command, bool expected)
    {
        PendingCommandCounter.IsChangeCommand(command!).Should().Be(expected);
    }
    [Theory]
    [InlineData("\"RMA-ROOM\" set level 1 -10.0", "RMA-ROOM")]
    [InlineData("\"Room 1 Level\" increment level 1 2.0", "Room 1 Level")]
    [InlineData("RoomCombiner1 set levelOut 2 -6", "RoomCombiner1")]     // unquoted pass-through
    [InlineData("  \"RMA-ROOM\" DECREMENT LEVEL 1 1", "RMA-ROOM")]       // case-insensitive
    [InlineData("\"RMA-ROOM\" set mute 1 true", null)]                   // not a level
    [InlineData("\"RMA-ROOM\" get level 1", null)]                       // a read
    [InlineData("\"RMA-ROOM\" set levelOutMax 1 0", null)]               // a different attribute
    [InlineData("DEVICE recallPreset 1001", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TryGetRawLevelTarget_Finds_The_Tag_Of_A_Raw_Level_Change(string? command, string? expectedTag)
    {
        var found = PendingCommandCounter.TryGetRawLevelTarget(command!, out var tag);
        found.Should().Be(expectedTag != null);
        tag.Should().Be(expectedTag);
    }

    [Fact]
    public void RawKey_Is_Case_Insensitive()
    {
        PendingCommandCounter.RawKey("rma-room").Should().Be(PendingCommandCounter.RawKey("RMA-ROOM"));
    }
}

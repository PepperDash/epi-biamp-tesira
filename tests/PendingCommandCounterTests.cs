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
    [InlineData("\"RMA-ROOM\" set level 1 -10.0", "RMA-ROOM", "level", 1)]
    [InlineData("\"Room 1 Level\" increment level 3 2.0", "Room 1 Level", "level", 3)]
    [InlineData("RoomCombiner1 set levelOut 2 -6", "RoomCombiner1", "levelOut", 2)]   // unquoted pass-through
    [InlineData("  \"RMA-ROOM\" DECREMENT LEVEL 1 1", "RMA-ROOM", "LEVEL", 1)]       // case-insensitive
    [InlineData("\"RMA-ROOM\" set mute 1 true", null, null, 0)]                      // not a level
    [InlineData("\"RMA-ROOM\" get level 1", null, null, 0)]                          // a read
    [InlineData("\"RMA-ROOM\" set levelOutMax 1 0", null, null, 0)]                  // a different attribute
    [InlineData("\"RMA-ROOM\" set level -10", null, null, 0)]                        // no channel
    [InlineData("DEVICE recallPreset 1001", null, null, 0)]
    [InlineData("", null, null, 0)]
    [InlineData(null, null, null, 0)]
    public void TryGetRawLevelTarget_Finds_The_Tag_Attribute_And_Channel(string? command, string? expectedTag, string? expectedAttribute, int expectedIndex)
    {
        var found = PendingCommandCounter.TryGetRawLevelTarget(command!, out var tag, out var attribute, out var index);
        found.Should().Be(expectedTag != null);
        tag.Should().Be(expectedTag);
        attribute.Should().Be(expectedAttribute);
        index.Should().Be(expectedIndex);
    }

    [Fact]
    public void RawKey_Is_Case_Insensitive_And_Per_Channel()
    {
        PendingCommandCounter.RawKey("rma-room", "LEVEL", 1).Should().Be(PendingCommandCounter.RawKey("RMA-ROOM", "level", 1));
        PendingCommandCounter.RawKey("LevelBlock", "level", 1).Should().NotBe(PendingCommandCounter.RawKey("LevelBlock", "level", 2));
        PendingCommandCounter.RawKey("Rc", "level", 2).Should().NotBe(PendingCommandCounter.RawKey("Rc", "levelOut", 2));
    }
}

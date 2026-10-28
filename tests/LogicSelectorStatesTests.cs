using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class LogicSelectorStatesTests
{
    [Theory]
    [InlineData("[false false false false]", 0)]
    [InlineData("[true false false false]", 1)]
    [InlineData("[false true false false]", 2)]
    [InlineData("[false false false true]", 4)]
    [InlineData("[false true true false]", 2)] // not expected (the block interlocks); the first wins
    [InlineData("  [false  TRUE false]  ", 2)]
    [InlineData("[]", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void ActiveChannel_Returns_The_1_Based_True_Channel_Or_0(string? value, int expected)
    {
        LogicSelectorStates.ActiveChannel(value!).Should().Be(expected);
    }
}

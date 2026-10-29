using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class RouterProtocolTests
{
    [Theory]
    [InlineData(3u, 2u, "\"Router1\" set input 3 2")]
    [InlineData(1u, 0u, "\"Router1\" set input 1 0")] // input 0 clears the output
    [InlineData(64u, 12u, "\"Router1\" set input 64 12")]
    public void SetCommand_Quotes_The_Tag_And_Sends_Output_Then_Input(uint output, uint input, string expected)
    {
        RouterProtocol.SetCommand("Router1", output, input).Should().Be(expected);
    }

    [Fact]
    public void Get_Subscribe_And_Unsubscribe_Name_The_Output()
    {
        RouterProtocol.GetCommand("Router 1", 4).Should().Be("\"Router 1\" get input 4");
        RouterProtocol.SubscribeCommand("Router1", 4, "Router1__Router-4", 250)
            .Should().Be("\"Router1\" subscribe input 4 Router1__Router-4 250");
        RouterProtocol.UnsubscribeCommand("Router1", 4, "Router1__Router-4")
            .Should().Be("\"Router1\" unsubscribe input 4 Router1__Router-4");
    }

    [Theory]
    [InlineData("Router1__Router-1", true, 1u)]
    [InlineData("Router1__Router-64", true, 64u)]
    [InlineData("Router1__Router", false, 0u)]     // the old single name
    [InlineData("Router1__Router-0", false, 0u)]   // outputs count from 1
    [InlineData("Router1__Router-x", false, 0u)]
    [InlineData("Router1__Router--2", false, 0u)]
    [InlineData("Router10__Router-2", false, 0u)]  // another router's name
    [InlineData("", false, 0u)]
    [InlineData(null, false, 0u)]
    public void TryParseSubscriptionOutput_Reads_The_Output_From_The_Name(string? name, bool ok, uint output)
    {
        RouterProtocol.TryParseSubscriptionOutput(name!, "Router1__Router", out var parsed).Should().Be(ok);
        parsed.Should().Be(output);
    }

    [Fact]
    public void SubscriptionName_Round_Trips()
    {
        var name = RouterProtocol.SubscriptionName("Router1__Router", 12);
        RouterProtocol.TryParseSubscriptionOutput(name, "Router1__Router", out var output).Should().BeTrue();
        output.Should().Be(12);
    }

    [Fact]
    public void GetAttributeCode_Round_Trips()
    {
        RouterProtocol.TryParseGetOutput(RouterProtocol.GetAttributeCode(7), out var output).Should().BeTrue();
        output.Should().Be(7);

        RouterProtocol.TryParseGetOutput("input", out _).Should().BeFalse();
        RouterProtocol.TryParseGetOutput(null!, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("2", true, 2u)]
    [InlineData("0", true, 0u)]        // cleared
    [InlineData(" 12 ", true, 12u)]
    [InlineData("+OK 3", true, 3u)]    // get reply
    [InlineData("+OK 0", true, 0u)]
    [InlineData("-1", false, 0u)]
    [InlineData("abc", false, 0u)]
    [InlineData("", false, 0u)]
    [InlineData(null, false, 0u)]
    public void TryParseInput_Accepts_A_Single_Value(string? value, bool ok, uint input)
    {
        RouterProtocol.TryParseInput(value!, out var parsed).Should().Be(ok);
        parsed.Should().Be(input);
    }
}

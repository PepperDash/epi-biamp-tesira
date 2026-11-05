using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class TtpReplyTests
{
    [Theory]
    [InlineData("-ERR address not found: [0 0 0]", true)]
    [InlineData("-ERR ALREADY_SUBSCRIBED", true)]
    [InlineData("-CANNOT_DELIVER", true)]
    [InlineData("-CANNOT_DELIVER ", true)]
    [InlineData("-GENERAL_FAILURE", true)]
    [InlineData("-ERR", true)]
    [InlineData("+OK", false)]
    [InlineData("+OK \"value\":-12.5", false)]
    [InlineData("\"Level1\" get level 1", false)]            // a command echo
    [InlineData("\"-ERR preset\" recallPresetByName", false)] // an echo whose tag contains -ERR
    [InlineData("! \"publishToken\":\"x\" \"value\":1", false)] // a publish
    [InlineData("-12.5", false)]                             // a bare number
    [InlineData("-err lower case", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsError_Recognizes_Every_Ttp_Error_Token(string? line, bool expected)
    {
        TtpReply.IsError(line).Should().Be(expected);
    }

    [Theory]
    [InlineData("-CANNOT_DELIVER", true)]
    [InlineData("-CANNOT_DELIVER ", true)]
    [InlineData("-CANNOT_DELIVERY", false)]
    [InlineData("-ERR CANNOT_DELIVER", false)]
    [InlineData("-GENERAL_FAILURE", false)]
    [InlineData("+OK", false)]
    [InlineData(null, false)]
    public void IsUnavailable_Is_Only_Cannot_Deliver(string? line, bool expected)
    {
        TtpReply.IsUnavailable(line).Should().Be(expected);
    }
}

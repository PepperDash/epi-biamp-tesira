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
    [InlineData("CANNOT_DELIVER", true)]                     // firmware 5.6.1.2: no leading minus
    [InlineData("CANNOT_DELIVER ", true)]
    [InlineData("GENERAL_FAILURE", true)]
    [InlineData("CANNOT_DELIVERY", false)]
    [InlineData("CANNOT_DELIVER get dndEnable 1", false)]    // echo of a dialer whose block is named CANNOT_DELIVER
    [InlineData("GENERAL_FAILURE set autoAnswer 1 true", false)]
    [InlineData("CANNOT_DELIVER: detail", false)]            // a bare token must fill the line
    [InlineData("-CANNOT_DELIVER: detail", true)]            // a signed one keeps its detail
    [InlineData("SESSION get aliases", false)]               // keyword command echoes
    [InlineData("SESSION set verbose false", false)]
    [InlineData("DEVICE get version", false)]
    [InlineData("ERR address not found", false)]             // only the known tokens go without a minus
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
    [InlineData("CANNOT_DELIVER", true)]                     // firmware 5.6.1.2: no leading minus
    [InlineData("CANNOT_DELIVER\r", true)]
    [InlineData("-CANNOT_DELIVERY", false)]
    [InlineData("CANNOT_DELIVERY", false)]
    [InlineData("-ERR CANNOT_DELIVER", false)]
    [InlineData("-GENERAL_FAILURE", false)]
    [InlineData("GENERAL_FAILURE", false)]
    [InlineData("+OK", false)]
    [InlineData(null, false)]
    public void IsUnavailable_Is_Only_Cannot_Deliver(string? line, bool expected)
    {
        TtpReply.IsUnavailable(line).Should().Be(expected);
    }
}

public class TtpReplyTargetTests
{
    [Theory]
    [InlineData("\"Level1\" get level 1", "Level1")]
    [InlineData("\"RMA-ROOM\" subscribe mute 1 RMA-ROOM__mute1 500", "RMA-ROOM")]
    [InlineData("\"Level 1\" set level 1 0", "Level 1")]
    [InlineData("DEVICE get activeFaultList", "DEVICE")]
    [InlineData("  \"Mixer\" get crosspoint 1 1", "Mixer")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void TargetOf_Returns_The_Commands_Instance_Tag(string? command, string expected)
    {
        TtpReply.TargetOf(command).Should().Be(expected);
    }
}

public class TtpReplyEchoTests
{
    [Theory]
    [InlineData("-DIALER1 get dndEnable 1", "-DIALER1 get dndEnable 1", true)]           // #214
    [InlineData("-DIALER1 get dndEnable 1\r", "-DIALER1 get dndEnable 1\r\n", true)]     // line endings
    [InlineData("  \"Level1\" get level 1 ", "\"Level1\" get level 1", true)]
    [InlineData("-ERR address not found: {\"deviceId\":0}", "-DIALER1 get dndEnable 1", false)]
    [InlineData("CANNOT_DELIVER", "-DIALER1 get dndEnable 1", false)]
    [InlineData("-dialer1 get dndEnable 1", "-DIALER1 get dndEnable 1", false)]          // case matters
    [InlineData("", "", false)]
    [InlineData(null, "-DIALER1 get dndEnable 1", false)]
    [InlineData("-DIALER1 get dndEnable 1", null, false)]
    public void IsEchoOf_Matches_The_Command_Text_Only(string? line, string? command, bool expected)
    {
        TtpReply.IsEchoOf(line, command).Should().Be(expected);
    }

    [Fact]
    public void A_Signed_Tag_Echo_Looks_Like_An_Error_So_The_Echo_Check_Must_Come_First()
    {
        const string command = "-DIALER1 get dndEnable 1";
        TtpReply.IsError(command).Should().BeTrue("this is why the reply handler checks for the echo first");
        TtpReply.IsEchoOf(command, command).Should().BeTrue();
    }
}

public class TtpReplyVerbTests
{
    [Theory]
    [InlineData("\"Level1\" get level 1", "get")]
    [InlineData("\"RMA-ROOM\" subscribe mute 1 RMA-ROOM__mute1 500", "subscribe")]
    [InlineData("\"Level 1\" set level 1 0", "set")]
    [InlineData("DEVICE get activeFaultList", "get")]
    [InlineData("\"Dial1\" dial 1 1 5551212", "dial")]
    [InlineData("\"Level1\"", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void VerbOf_Returns_The_Word_After_The_Instance_Tag(string? command, string expected)
    {
        TtpReply.VerbOf(command).Should().Be(expected);
    }

    [Theory]
    [InlineData("+OK", true)]
    [InlineData("+OK \"value\":-12.000000", true)]
    [InlineData("-ERR ALREADY_SUBSCRIBED", true)]
    [InlineData("-ERR address not found: [0 0 0]", false)]
    [InlineData("-CANNOT_DELIVER", false)]
    [InlineData("-GENERAL_FAILURE", false)]
    [InlineData(null, false)]
    public void IsSuccess_Is_Ok_Or_Already_Subscribed(string? line, bool expected)
    {
        TtpReply.IsSuccess(line).Should().Be(expected);
    }
}

using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class TesiraFaultListTests
{
    // Captured from a TesiraFORTÉ X 400 (firmware 5.7.1.4) with an inactive Dante flow.
    private const string MajorDanteFault =
        "+OK \"value\":[{\"id\":INDICATOR_MAJOR_IN_DEVICE \"name\":\"Major Fault in Device\" \"faults\":[{\"id\":FAULT_DANTE_FLOW_INACTIVE \"name\":\"one or more Dante flows inactive\" \"instance\":\"DEFAULT\"}] \"serialNumber\":\"05398476\"} {\"id\":INDICATOR_MAJOR_IN_SYSTEM \"name\":\"Major Fault in System\" \"faults\":[] \"serialNumber\":\"05398476\"}]";

    private const string NoFault =
        "+OK \"value\":[{\"id\":INDICATOR_NONE_IN_DEVICE \"name\":\"No fault in device\" \"faults\":[] \"serialNumber\":\"05398476\"}]";

    [Fact]
    public void Parses_The_Hardware_Reply()
    {
        var list = TesiraFaultList.Parse(MajorDanteFault)!;

        list.Indicators.Should().HaveCount(2);
        list.Indicators[0].Id.Should().Be("INDICATOR_MAJOR_IN_DEVICE");
        list.Indicators[0].Faults.Should().ContainSingle();
        list.Indicators[0].Faults[0].Id.Should().Be("FAULT_DANTE_FLOW_INACTIVE");
        list.Indicators[0].Faults[0].Instance.Should().Be("DEFAULT");

        list.HasFault.Should().BeTrue();
        list.ActiveFaultCount.Should().Be(1);
        list.Severity.Should().Be(2);
        list.FaultId.Should().Be("INDICATOR_MAJOR_IN_DEVICE"); // first on a tie: device before system
        list.FaultName.Should().Be("Major Fault in Device");
        list.FaultListText.Should().Be("one or more Dante flows inactive");
    }

    // The same fault as the plugin's own (terse) session receives it, from the PRO4 log.
    private const string MajorDanteFaultTerse =
        "+OK [[1 \"Major Fault in Device\" [[86 \"one or more Dante flows inactive\" \"DEFAULT\"]] \"05398476\"] [3 \"Major Fault in System\" [] \"05398476\"]]";

    [Fact]
    public void Parses_The_Terse_Hardware_Reply_Like_The_Verbose_One()
    {
        var terse = TesiraFaultList.Parse(MajorDanteFaultTerse)!;
        var verbose = TesiraFaultList.Parse(MajorDanteFault)!;

        terse.Indicators.Should().HaveCount(2);
        terse.Indicators[1].Id.Should().Be("INDICATOR_MAJOR_IN_SYSTEM");
        terse.Indicators[0].Faults[0].Id.Should().Be("86");
        terse.Indicators[0].Faults[0].Instance.Should().Be("DEFAULT");

        terse.HasFault.Should().Be(verbose.HasFault);
        terse.ActiveFaultCount.Should().Be(verbose.ActiveFaultCount);
        terse.Severity.Should().Be(verbose.Severity);
        terse.FaultId.Should().Be(verbose.FaultId);
        terse.FaultName.Should().Be(verbose.FaultName);
        terse.FaultListText.Should().Be(verbose.FaultListText);
    }

    [Theory]
    [InlineData("+OK [[0 \"No fault in device\" [] \"05398476\"]]", 0, "INDICATOR_NONE_IN_DEVICE")]
    [InlineData("+OK [[5 \"Minor Fault in System\" [[12 \"x\" \"A\"]] \"1\"]]", 1, "INDICATOR_MINOR_IN_SYSTEM")]
    [InlineData("+OK [[9 \"Critical Fault in Device\" [[13 \"y\" \"B\"]] \"1\"]]", 3, "INDICATOR_CRITICAL_IN_DEVICE")]
    [InlineData("+OK [[7 \"Something else\" [[14 \"z\" \"C\"]] \"1\"]]", 1, "7")] // unknown text: keep the number, still a fault
    public void Terse_Indicators_Are_Named_From_Their_Text(string reply, int severity, string id)
    {
        var list = TesiraFaultList.Parse(reply)!;

        list.Severity.Should().Be(severity);
        list.FaultId.Should().Be(id);
    }

    [Fact]
    public void No_Fault_Reports_The_None_Indicator_Like_UDM()
    {
        var list = TesiraFaultList.Parse(NoFault)!;

        list.HasFault.Should().BeFalse();
        list.ActiveFaultCount.Should().Be(0);
        list.Severity.Should().Be(0);
        list.FaultId.Should().Be("INDICATOR_NONE_IN_DEVICE");
        list.FaultName.Should().Be("No fault in device");
        list.FaultListText.Should().BeEmpty();
    }

    [Fact]
    public void Worst_Indicator_Wins_And_Faults_Are_Counted_Across_Indicators()
    {
        const string reply =
            "+OK \"value\":[{\"id\":INDICATOR_MINOR_IN_DEVICE \"name\":\"Minor Fault in Device\" \"faults\":[{\"id\":FAULT_A \"name\":\"fault a\" \"instance\":\"1\"} {\"id\":FAULT_B \"name\":\"fault, with comma [and brackets]\" \"instance\":\"2\"}]} {\"id\":INDICATOR_CRITICAL_IN_SYSTEM \"name\":\"Critical Fault in System\" \"faults\":[{\"id\":FAULT_A \"name\":\"fault a\" \"instance\":\"3\"}]}]";

        var list = TesiraFaultList.Parse(reply)!;

        list.ActiveFaultCount.Should().Be(3);
        list.Severity.Should().Be(3);
        list.FaultId.Should().Be("INDICATOR_CRITICAL_IN_SYSTEM");
        list.FaultListText.Should().Be("fault a, fault, with comma [and brackets]"); // de-duplicated
    }

    [Fact]
    public void Signature_Changes_Only_With_The_Active_Faults()
    {
        var a = TesiraFaultList.Parse(MajorDanteFault)!;
        var b = TesiraFaultList.Parse(MajorDanteFault)!;
        var c = TesiraFaultList.Parse(NoFault)!;

        a.Signature.Should().Be(b.Signature);
        a.Signature.Should().NotBe(c.Signature);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DEVICE get activeFaultList")] // the SSH echo
    [InlineData("-ERR operation subscribe not supported on attribute: activeFaultList")]
    [InlineData("+OK")]
    [InlineData("+OK \"value\":\"5.7.1.4\"")]
    public void Returns_Null_For_Anything_But_A_Value_List(string? reply)
    {
        TesiraFaultList.Parse(reply!).Should().BeNull();
    }

    [Fact]
    public void Unknown_Indicator_With_Faults_Counts_As_A_Fault()
    {
        var list = TesiraFaultList.Parse(
            "+OK \"value\":[{\"id\":INDICATOR_SOMETHING_NEW \"name\":\"New\" \"faults\":[{\"id\":FAULT_X \"name\":\"x\"}]}]")!;

        list.HasFault.Should().BeTrue();
        list.Severity.Should().Be(1);
    }

    [Fact]
    public void Empty_List_Is_No_Fault()
    {
        var list = TesiraFaultList.Parse("+OK \"value\":[]")!;

        list.HasFault.Should().BeFalse();
        list.FaultId.Should().BeEmpty();
    }
}

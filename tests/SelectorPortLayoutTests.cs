using FluentAssertions;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Routing;
using PepperDash.Essentials.Core;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

/// <summary>
/// The ports the real source selector / switcher and the mock build from config (shared layout), and
/// the mock's numbering of its string-keyed switcher config.
/// </summary>
public class SelectorPortLayoutTests
{
    private static SelectorPortLayout SourceSelector(string json) =>
        SelectorPortLayout.ForSourceSelector(
            JsonConvert.DeserializeObject<TesiraSourceSelectorControlBlockConfig>(json)!.SourceSelectorInputs);

    private static SelectorPortLayout RealSwitcher(string json)
    {
        var config = JsonConvert.DeserializeObject<TesiraSwitcherControlBlockConfig>(json)!;
        return SelectorPortLayout.ForSwitcher(config.SwitcherInputs, config.SwitcherOutputs);
    }

    private static SelectorPortLayout MockSwitcher(string json) =>
        MockSelectorConfig.ForSwitcher(JsonConvert.DeserializeObject<TesiraDspMockSourceSelectorConfig>(json));

    private static IEnumerable<(uint, string)> Ports(IEnumerable<SelectorPort> ports) =>
        ports.Select(p => (p.Index, p.Label));

    // ── Source selector ──────────────────────────────────────────────────

    [Fact]
    public void SourceSelector_Has_The_Configured_Inputs_Then_None_And_One_Output()
    {
        var layout = SourceSelector(@"{ ""sourceSelectorInputs"": { ""1"": { ""label"": ""Codec"" }, ""2"": { ""label"": ""Music"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "Codec"), (2u, "Music"), (0u, "None"));
        Ports(layout.Outputs).Should().Equal((1u, "output"));
        layout.Problems.Should().BeEmpty();
        layout.InitialInput.Should().Be(0);
    }

    [Fact]
    public void SourceSelector_Without_Inputs_Still_Has_None_And_The_Output()
    {
        var layout = SourceSelector(@"{ ""label"": ""Empty"" }");

        Ports(layout.Inputs).Should().Equal((0u, "None"));
        layout.Outputs.Should().ContainSingle();
    }

    [Fact]
    public void Input_Zero_Is_Skipped_As_Reserved_For_None()
    {
        var layout = SourceSelector(@"{ ""sourceSelectorInputs"": { ""0"": { ""label"": ""Silence"" }, ""1"": { ""label"": ""Codec"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "Codec"), (0u, "None"));
        layout.Problems.Should().ContainSingle().Which.Should().Contain("reserved for None");
    }

    [Fact]
    public void Unlabelled_Input_Is_Skipped()
    {
        var layout = SourceSelector(@"{ ""sourceSelectorInputs"": { ""1"": {}, ""2"": { ""label"": ""Music"" } } }");

        Ports(layout.Inputs).Should().Equal((2u, "Music"), (0u, "None"));
        layout.Problems.Should().ContainSingle().Which.Should().Contain("no label");
    }

    [Fact]
    public void Duplicate_Label_Is_Skipped_Because_Labels_Are_Port_Keys()
    {
        var layout = SourceSelector(@"{ ""sourceSelectorInputs"": { ""1"": { ""label"": ""Codec"" }, ""2"": { ""label"": ""Codec"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "Codec"), (0u, "None"));
        layout.Problems.Should().ContainSingle().Which.Should().Contain("already used");
    }

    [Fact]
    public void LabelFor_Reports_None_And_Unconfigured_Inputs()
    {
        var layout = SourceSelector(@"{ ""sourceSelectorInputs"": { ""1"": { ""label"": ""Codec"" } } }");

        layout.LabelFor(1).Should().Be("Codec");
        layout.LabelFor(0).Should().Be("None");
        layout.LabelFor(4).Should().Be("Input 4");
    }

    // ── Switcher, real format ────────────────────────────────────────────

    [Fact]
    public void Switcher_Keeps_Input_And_Output_Numbers_And_Has_No_None()
    {
        var layout = RealSwitcher(@"{ ""switcherInputs"": { ""1"": { ""label"": ""Laptop"" }, ""3"": { ""label"": ""Codec"" } },
            ""switcherOutputs"": { ""1"": { ""label"": ""Speakers"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "Laptop"), (3u, "Codec"));
        Ports(layout.Outputs).Should().Equal((1u, "Speakers"));
        layout.Problems.Should().BeEmpty();
    }

    [Fact]
    public void Switcher_Without_Inputs_Or_Outputs_Has_No_Ports()
    {
        var layout = RealSwitcher(@"{ ""label"": ""Empty"" }");

        layout.Inputs.Should().BeEmpty();
        layout.Outputs.Should().BeEmpty();
    }

    [Fact]
    public void Switcher_Input_Zero_Is_Skipped()
    {
        var layout = RealSwitcher(@"{ ""switcherInputs"": { ""0"": { ""label"": ""Off"" }, ""1"": { ""label"": ""Laptop"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "Laptop"));
        layout.Problems.Should().ContainSingle();
    }

    // ── Signal types ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(eRoutingSignalType.Audio, true, true)]
    [InlineData(eRoutingSignalType.AudioVideo, true, false)]
    [InlineData(eRoutingSignalType.Video, false, false)]
    public void Source_Selector_Takes_Any_Audio_Switcher_Only_Exactly_Audio(eRoutingSignalType type, bool selector, bool switcher)
    {
        SourceSelector("{}").AcceptsSignalType(type).Should().Be(selector);
        RealSwitcher("{}").AcceptsSignalType(type).Should().Be(switcher);
    }

    // ── Mock switcher numbering ──────────────────────────────────────────

    [Fact]
    public void Mock_Sources_Are_Numbered_In_Config_Order_With_Their_Keys_As_Items()
    {
        var layout = MockSwitcher(@"{ ""initialSource"": ""source2"",
            ""sources"": { ""source1"": { ""label"": ""Source 1"" }, ""source2"": { ""label"": ""Source 2"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "Source 1"), (2u, "Source 2"));
        layout.Inputs.Select(i => i.ItemKey).Should().Equal("source1", "source2");
        layout.InitialInput.Should().Be(2);
    }

    [Fact]
    public void Mock_Numeric_Keys_Keep_Their_Numbers_And_Others_Take_Unused_Ones()
    {
        var layout = MockSwitcher(@"{ ""sources"": { ""2"": { ""label"": ""Codec"" }, ""aux"": { ""label"": ""Aux"" }, ""1x"": { ""label"": ""Spare"" } } }");

        Ports(layout.Inputs).Should().Equal((2u, "Codec"), (1u, "Aux"), (3u, "Spare"));
        layout.Problems.Should().BeEmpty();
    }

    [Fact]
    public void Mock_Leading_Zero_Key_Duplicating_A_Number_Is_Skipped()
    {
        var layout = MockSwitcher(@"{ ""sources"": { ""1"": { ""label"": ""Codec"" }, ""01"": { ""label"": ""Music"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "Codec"));
        layout.Problems.Should().ContainSingle().Which.Should().Contain("'01'");
    }

    [Fact]
    public void Mock_Sources_Without_Labels_Use_Their_Keys()
    {
        var layout = MockSwitcher(@"{ ""sources"": { ""laptop"": {} } }");

        Ports(layout.Inputs).Should().Equal((1u, "laptop"));
    }

    [Fact]
    public void Mock_Real_Format_Inputs_Need_Labels_As_On_The_Real_Switcher()
    {
        var layout = MockSwitcher(@"{ ""switcherInputs"": { ""1"": {}, ""2"": { ""label"": ""Codec"" } }, ""switcherOutputs"": { ""1"": { ""label"": ""Program"" } } }");

        Ports(layout.Inputs).Should().Equal((2u, "Codec"));
        Ports(layout.Outputs).Should().Equal((1u, "Program"));
        layout.Problems.Should().ContainSingle().Which.Should().Contain("no label");
    }

    [Fact]
    public void Mock_Sources_Win_Over_SwitcherInputs()
    {
        var layout = MockSwitcher(@"{ ""sources"": { ""a"": { ""label"": ""A"" } }, ""switcherInputs"": { ""1"": { ""label"": ""X"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "A"));
    }

    [Fact]
    public void Mock_Unknown_InitialSource_Starts_Cleared_And_Is_Reported()
    {
        var layout = MockSwitcher(@"{ ""initialSource"": ""nope"", ""sources"": { ""a"": { ""label"": ""A"" } } }");

        layout.InitialInput.Should().Be(0);
        layout.Problems.Should().ContainSingle().Which.Should().Contain("initialSource 'nope'");
    }
}

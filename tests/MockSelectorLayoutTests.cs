using FluentAssertions;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class MockSelectorLayoutTests
{
    private static MockSelectorLayout SourceSelector(string json) =>
        MockSelectorLayout.ForSourceSelector(JsonConvert.DeserializeObject<TesiraSourceSelectorControlBlockConfig>(json));

    private static MockSelectorLayout Switcher(string json) =>
        MockSelectorLayout.ForSwitcher(JsonConvert.DeserializeObject<TesiraDspMockSourceSelectorConfig>(json));

    private static IEnumerable<(uint, string)> Ports(IEnumerable<MockSelectorPort> ports) =>
        ports.Select(p => (p.Index, p.Label));

    // ── sourceSelectorControlBlocks (real TesiraDspSourceSelector) ───────

    [Fact]
    public void SourceSelector_Has_The_Configured_Inputs_Then_None()
    {
        var layout = SourceSelector(@"{ ""sourceSelectorInstanceTag"": ""ProgramSelect"",
            ""sourceSelectorInputs"": { ""1"": { ""label"": ""Codec"" }, ""2"": { ""label"": ""Music"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "Codec"), (2u, "Music"), (0u, "None"));
    }

    [Fact]
    public void SourceSelector_Has_One_Output_Named_output()
    {
        var layout = SourceSelector(@"{ ""sourceSelectorInputs"": { ""1"": { ""label"": ""Codec"" } } }");

        Ports(layout.Outputs).Should().Equal((1u, "output"));
    }

    [Fact]
    public void SourceSelector_Starts_On_None()
    {
        var layout = SourceSelector(@"{ ""sourceSelectorInputs"": { ""1"": { ""label"": ""Codec"" } } }");

        layout.InitialInput.Should().Be(0);
        layout.LabelFor(0).Should().Be("None");
    }

    [Fact]
    public void SourceSelector_Ignores_A_Configured_Input_Zero()
    {
        var layout = SourceSelector(@"{ ""sourceSelectorInputs"": { ""0"": { ""label"": ""Silence"" }, ""1"": { ""label"": ""Codec"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "Codec"), (0u, "None"));
    }

    [Fact]
    public void SourceSelector_Without_Inputs_Still_Has_None_And_The_Output()
    {
        var layout = SourceSelector(@"{ ""label"": ""Empty"" }");

        Ports(layout.Inputs).Should().Equal((0u, "None"));
        layout.Outputs.Should().ContainSingle();
    }

    [Fact]
    public void Unlabelled_Inputs_Are_Named_By_Number()
    {
        var layout = SourceSelector(@"{ ""sourceSelectorInputs"": { ""3"": {} } }");

        layout.Inputs[0].Label.Should().Be("Input 3");
        layout.LabelFor(7).Should().Be("Input 7");
    }

    // ── switcherControlBlocks (real TesiraDspSwitcher) ───────────────────

    [Fact]
    public void Switcher_Real_Format_Keeps_Input_And_Output_Numbers_And_Has_No_None()
    {
        var layout = Switcher(@"{ ""switcherInputs"": { ""1"": { ""label"": ""Laptop"" }, ""3"": { ""label"": ""Codec"" } },
            ""switcherOutputs"": { ""1"": { ""label"": ""Speakers"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "Laptop"), (3u, "Codec"));
        Ports(layout.Outputs).Should().Equal((1u, "Speakers"));
    }

    [Fact]
    public void Switcher_Mock_Format_Numbers_Sources_In_Config_Order()
    {
        var layout = Switcher(@"{ ""initialSource"": ""source2"",
            ""sources"": { ""source1"": { ""label"": ""Source 1"" }, ""source2"": { ""label"": ""Source 2"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "Source 1"), (2u, "Source 2"));
        layout.Inputs.Select(i => i.ItemKey).Should().Equal("source1", "source2");
        layout.InitialInput.Should().Be(2);
    }

    [Fact]
    public void Switcher_Sources_Win_Over_SwitcherInputs()
    {
        var layout = Switcher(@"{ ""sources"": { ""a"": { ""label"": ""A"" } }, ""switcherInputs"": { ""1"": { ""label"": ""X"" } } }");

        Ports(layout.Inputs).Should().Equal((1u, "A"));
    }

    [Fact]
    public void Switcher_Without_Outputs_Has_No_Output_Ports()
    {
        var layout = Switcher(@"{ ""sources"": { ""a"": { ""label"": ""A"" } } }");

        layout.Outputs.Should().BeEmpty();
    }

    [Fact]
    public void Switcher_Unknown_InitialSource_Starts_Cleared()
    {
        var layout = Switcher(@"{ ""initialSource"": ""nope"", ""sources"": { ""a"": { ""label"": ""A"" } } }");

        layout.InitialInput.Should().Be(0);
    }
}

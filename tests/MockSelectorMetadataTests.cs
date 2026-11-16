using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class MockSelectorMetadataTests
{
    private const string MockNs = "Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock.";
    private const string RealNs = "Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.";

    [Theory]
    [InlineData("IRoutingWithFeedback")]
    [InlineData("IRoutingMidpointWithFeedback")]
    [InlineData("IHasNamedRoutingSlots")]
    public void MockSelector_Implements_The_Routing_Interfaces_Of_The_Real_Selector_And_Switcher(string interfaceName)
    {
        var mock = AssemblyFixture.PluginAssembly.GetType(MockNs + "TesiraDspMockSourceSelector");
        mock.Should().NotBeNull();
        mock!.GetInterfaces().Should().Contain(i => i.Name == interfaceName);

        foreach (var real in new[] { "TesiraDspSourceSelector", "TesiraDspSwitcher" })
            AssemblyFixture.PluginAssembly.GetType(RealNs + real)!.GetInterfaces()
                .Should().Contain(i => i.Name == interfaceName, $"the mock should match {real}");
    }

    [Fact]
    public void MockSelector_Still_Lists_Its_Inputs()
    {
        var type = AssemblyFixture.PluginAssembly.GetType(MockNs + "TesiraDspMockSourceSelector");
        type!.GetInterfaces().Should().Contain(i => i.Name == "IHasInputs`1");
    }

    [Fact]
    public void MockConfig_Takes_The_Real_SourceSelectorControlBlocks_Format()
    {
        var config = AssemblyFixture.PluginAssembly.GetType(MockNs + "TesiraDspMockPropertiesConfig");
        var property = config!.GetProperty("SourceSelectorControlBlocks");

        property.Should().NotBeNull();
        property!.PropertyType.GetGenericArguments()[1].Name.Should().Be("TesiraSourceSelectorControlBlockConfig");
    }

    [Fact]
    public void MockSwitcherConfig_Takes_SwitcherOutputs()
    {
        var config = AssemblyFixture.PluginAssembly.GetType(MockNs + "TesiraDspMockSourceSelectorConfig");
        config!.GetProperty("SwitcherOutputs").Should().NotBeNull();
    }

    [Theory]
    [InlineData("SimulateSelection", 1)]
    [InlineData("ExecuteSwitch", 3)]
    [InlineData("ClearRoute", 2)]
    [InlineData("FireUpdate", 0)]
    public void MockSelector_Has_Its_Bench_And_Routing_Methods(string method, int parameterCount)
    {
        var type = AssemblyFixture.PluginAssembly.GetType(MockNs + "TesiraDspMockSourceSelector");
        type!.GetMethod(method)!.GetParameters().Should().HaveCount(parameterCount);
    }
}

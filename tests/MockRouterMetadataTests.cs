using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class MockRouterMetadataTests
{
    private const string MockNs = "Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock.";

    [Theory]
    [InlineData("IRoutingWithFeedback")]
    [InlineData("IRoutingMidpointWithFeedback")]
    [InlineData("IHasNamedRoutingSlots")]
    public void MockRouter_Implements_The_Real_Routers_Interfaces(string interfaceName)
    {
        var type = AssemblyFixture.PluginAssembly.GetType(MockNs + "TesiraDspMockRouter");
        type.Should().NotBeNull();
        type!.GetInterfaces().Should().Contain(i => i.Name == interfaceName);
    }

    [Fact]
    public void MockConfig_Takes_The_Real_RouterControlBlocks_Format()
    {
        var config = AssemblyFixture.PluginAssembly.GetType(MockNs + "TesiraDspMockPropertiesConfig");
        var property = config!.GetProperty("RouterControlBlocks");

        property.Should().NotBeNull();
        property!.PropertyType.GetGenericArguments()[1].Name.Should().Be("TesiraRouterControlBlockConfig");
    }

    [Theory]
    [InlineData("SimulateRoute", 2)]
    [InlineData("ReportRoutes", 0)]
    [InlineData("GetOutputRoute", 1)]
    [InlineData("ClearRoute", 2)]
    public void MockRouter_Has_Its_Bench_And_Routing_Methods(string method, int parameterCount)
    {
        var type = AssemblyFixture.PluginAssembly.GetType(MockNs + "TesiraDspMockRouter");
        type!.GetMethod(method)!.GetParameters().Should().HaveCount(parameterCount);
    }
}

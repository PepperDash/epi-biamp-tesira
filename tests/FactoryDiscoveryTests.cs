using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class FactoryDiscoveryTests
{
    [Fact]
    public void Assembly_Loads_Successfully()
    {
        AssemblyFixture.PluginAssembly.Should().NotBeNull();
    }

    [Fact]
    public void Assembly_Name_Is_Expected()
    {
        AssemblyFixture.PluginAssembly.GetName().Name.Should().Be("Tesira-DSP-EPI.4Series");
    }

    [Fact]
    public void Factory_Count_Is_One()
    {
        // The real-device factory; the bench mock's factory (namespace ...Tesira.Mock) is counted separately.
        AssemblyFixture.FindFactoryTypes().Where(t => t.Namespace?.EndsWith(".Mock") != true).Should().HaveCount(1);
    }

    [Fact]
    public void MockFactory_Exists()
    {
        AssemblyFixture.FindFactoryTypes().Should().ContainSingle(t => t.Name == "TesiraDspMockFactory");
    }

    [Fact]
    public void TesiraFactory_Exists()
    {
        AssemblyFixture.FindFactoryTypes().Should().ContainSingle(t => t.Name == "TesiraFactory");
    }

    [Fact]
    public void Factory_Has_Parameterless_Constructor()
    {
        foreach (var factory in AssemblyFixture.FindFactoryTypes())
        {
            factory.GetConstructor(Type.EmptyTypes).Should()
                .NotBeNull($"Factory '{factory.Name}' must have a parameterless constructor");
        }
    }
}

using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class FactoryMetadataTests
{
    private static readonly Lazy<string> FactorySource = new(() =>
        AssemblyFixture.FindSourceForClass("TesiraFactory")
            ?? throw new FileNotFoundException("TesiraFactory source not found"));

    [Fact]
    public void Factory_Sets_MinimumEssentialsFrameworkVersion_To_3_0_0()
    {
        Regex.IsMatch(FactorySource.Value, @"MinimumEssentialsFrameworkVersion\s*=\s*""3\.0\.0""")
            .Should().BeTrue("TesiraFactory should set MinimumEssentialsFrameworkVersion to \"3.0.0\"");
    }

    [Fact]
    public void Factory_Sets_TypeNames()
    {
        Regex.IsMatch(FactorySource.Value, @"TypeNames\s*=\s*new\s+List<string>")
            .Should().BeTrue("TesiraFactory should set TypeNames in the constructor");
    }

    [Theory]
    [InlineData("tesira")]
    [InlineData("tesiraforte")]
    [InlineData("tesiraserver")]
    [InlineData("tesira-dsp")]
    [InlineData("tesiradsp")]
    public void Factory_Source_Contains_TypeName(string typeName)
    {
        FactorySource.Value.Should().Contain($"\"{typeName}\"",
            $"TesiraFactory should register type name \"{typeName}\"");
    }

    [Fact]
    public void No_Duplicate_TypeNames_In_Factory_Source()
    {
        var match = Regex.Match(FactorySource.Value, @"TypeNames\s*=\s*new\s+List<string>\s*\{([^}]+)\}");
        match.Success.Should().BeTrue("Should find TypeNames assignment");

        var quoted = Regex.Matches(match.Groups[1].Value, @"""([^""]+)""").Select(m => m.Groups[1].Value).ToList();
        quoted.Should().OnlyHaveUniqueItems("TypeNames should not contain duplicates");
    }
}

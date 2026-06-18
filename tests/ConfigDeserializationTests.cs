using FluentAssertions;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class ConfigDeserializationTests
{
    private const string Ns = "Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.";

    private static readonly Lazy<Type?> ConfigType = new(() =>
        AssemblyFixture.PluginAssembly.GetType(Ns + "TesiraDspPropertiesConfig"));

    [Fact]
    public void Config_Class_Exists()
    {
        ConfigType.Value.Should().NotBeNull("TesiraDspPropertiesConfig class should exist in the assembly");
    }

    [Theory]
    [InlineData("TesiraDspPropertiesConfig")]
    [InlineData("TesiraExpanderBlockConfig")]
    [InlineData("TesiraFaderControlBlockConfig")]
    [InlineData("TesiraDialerControlBlockConfig")]
    [InlineData("TesiraSwitcherControlBlockConfig")]
    [InlineData("TesiraRouterControlBlockConfig")]
    [InlineData("TesiraSourceSelectorControlBlockConfig")]
    [InlineData("TesiraStateControlBlockConfig")]
    [InlineData("TesiraMeterBlockConfig")]
    [InlineData("TesiraLogicMeterBlockConfig")]
    [InlineData("TesiraCrosspointStateBlockConfig")]
    [InlineData("TesiraRoomCombinerBlockConfig")]
    public void Config_Block_Class_Exists_And_Is_Constructible(string className)
    {
        var type = AssemblyFixture.PluginAssembly.GetType(Ns + className);
        type.Should().NotBeNull($"config class '{className}' should exist");
        type!.GetConstructor(Type.EmptyTypes).Should()
            .NotBeNull($"config class '{className}' must have a parameterless constructor for deserialization");
    }

    [Theory]
    [InlineData("faderControlBlocks")]
    [InlineData("dialerControlBlocks")]
    [InlineData("switcherControlBlocks")]
    [InlineData("routerControlBlocks")]
    [InlineData("sourceSelectorControlBlocks")]
    [InlineData("presets")]
    [InlineData("stateControlBlocks")]
    [InlineData("meterControlBlocks")]
    [InlineData("logicMeterControlBlocks")]
    [InlineData("crosspointStateControlBlocks")]
    [InlineData("roomCombinerControlBlocks")]
    [InlineData("tesiraExpanderBlocks")]
    [InlineData("resubscribeString")]
    public void Config_Property_Has_JsonPropertyAttribute(string jsonName)
    {
        HasJsonProperty(ConfigType.Value!, jsonName).Should()
            .BeTrue($"TesiraDspPropertiesConfig should have a property with [JsonProperty(\"{jsonName}\")]");
    }

    [Theory]
    [InlineData("hostname")]
    [InlineData("index")]
    public void ExpanderBlock_Property_Has_JsonPropertyAttribute(string jsonName)
    {
        var type = AssemblyFixture.PluginAssembly.GetType(Ns + "TesiraExpanderBlockConfig");
        type.Should().NotBeNull("TesiraExpanderBlockConfig class should exist");
        HasJsonProperty(type!, jsonName).Should()
            .BeTrue($"TesiraExpanderBlockConfig should have a property with [JsonProperty(\"{jsonName}\")]");
    }

    [Theory]
    [InlineData("FaderControlBlocks", "TesiraFaderControlBlockConfig")]
    [InlineData("DialerControlBlocks", "TesiraDialerControlBlockConfig")]
    [InlineData("ExpanderBlocks",      "TesiraExpanderBlockConfig")]
    public void Block_Property_Is_Dictionary_Of_Expected_Value(string propertyName, string expectedValueType)
    {
        var prop = ConfigType.Value!.GetProperty(propertyName);
        prop.Should().NotBeNull($"TesiraDspPropertiesConfig should expose {propertyName}");

        var type = prop!.PropertyType;
        type.IsGenericType.Should().BeTrue($"{propertyName} must be a generic dictionary");
        type.GetGenericTypeDefinition().Name.Should().Be("Dictionary`2",
            $"{propertyName} must be a Dictionary<string,T> so JSON objects deserialize correctly");
        type.GetGenericArguments()[0].Name.Should().Be("String");
        type.GetGenericArguments()[1].Name.Should().Be(expectedValueType);
    }

    private static bool HasJsonProperty(Type type, string jsonName) =>
        type.GetProperties().Any(p =>
            p.CustomAttributes.Any(a =>
                a.AttributeType.Name == "JsonPropertyAttribute"
                && a.ConstructorArguments.Any(arg =>
                    string.Equals(arg.Value?.ToString(), jsonName, StringComparison.Ordinal))));
}

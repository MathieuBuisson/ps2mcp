using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class ParameterDefinitionTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var aliases = ImmutableArray.Create("CN", "ComputerName");
        var parameterSets = ImmutableArray.Create("Default", "ByName");

        var parameter = new ParameterDefinition(
            Name: "Name",
            Type: "string",
            IsMandatory: true,
            IsSecure: false,
            Description: "The name.",
            DefaultValue: null,
            Aliases: aliases,
            ParameterSets: parameterSets);

        Assert.Equal("Name", parameter.Name);
        Assert.Equal("string", parameter.Type);
        Assert.True(parameter.IsMandatory);
        Assert.False(parameter.IsSecure);
        Assert.Equal("The name.", parameter.Description);
        Assert.Null(parameter.DefaultValue);
        Assert.Equal(aliases, parameter.Aliases);
        Assert.Equal(parameterSets, parameter.ParameterSets);
    }

    [Fact]
    public void IsSecure_DistinguishesSecureParameter()
    {
        var secure = new ParameterDefinition("Token", "SecureString", false, true, null, null, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
        var plain = new ParameterDefinition("Name", "string", false, false, null, null, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);

        Assert.True(secure.IsSecure);
        Assert.False(plain.IsSecure);
        Assert.NotEqual(secure, plain);
    }

    [Fact]
    public void ValueEquality_HoldsForIdenticalData()
    {
        var a = MakeParameter();
        var b = MakeParameter();

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueEquality_HoldsForDistinctArrayInstancesWithSameElements()
    {
        // Both Aliases and ParameterSets are distinct ImmutableArray<string> instances with element-identical content; structural equality must hold.
        var aliasesA = ImmutableArray.Create("CN", "ComputerName");
        var aliasesB = ImmutableArray.Create("CN", "ComputerName");
        var setsA = ImmutableArray.Create("Default", "ByName");
        var setsB = ImmutableArray.Create("Default", "ByName");
        var a = new ParameterDefinition("Name", "string", true, false, null, null, aliasesA, setsA);
        var b = new ParameterDefinition("Name", "string", true, false, null, null, aliasesB, setsB);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueInequality_WhenIsMandatoryDiffers()
    {
        var a = MakeParameter();
        var b = a with { IsMandatory = !a.IsMandatory };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void HashCode_DiffersWhenOnlyAliasesSequenceContentsDiffer()
    {
        // Regression: sequence contents must contribute to GetHashCode.
        var a = new ParameterDefinition("Name", "string", false, false, null, null, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
        var b = new ParameterDefinition("Name", "string", false, false, null, null, ImmutableArray.Create("CN", "ComputerName"), ImmutableArray<string>.Empty);

        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    private static ParameterDefinition MakeParameter() =>
        new(
            Name: "Name",
            Type: "string",
            IsMandatory: false,
            IsSecure: false,
            Description: null,
            DefaultValue: null,
            Aliases: ImmutableArray<string>.Empty,
            ParameterSets: ImmutableArray<string>.Empty);
}

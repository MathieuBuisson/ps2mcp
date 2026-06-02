using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class OutputMetadataTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var typeArgs = ImmutableArray.Create("string");

        var output = new OutputMetadata(
            OutputTypeName: "System.Collections.Generic.List",
            OutputTypeArguments: typeArgs);

        Assert.Equal("System.Collections.Generic.List", output.OutputTypeName);
        Assert.Equal(typeArgs, output.OutputTypeArguments);
    }

    [Fact]
    public void AllFields_CanBeNull()
    {
        var output = new OutputMetadata(null, null);

        Assert.Null(output.OutputTypeName);
        Assert.Null(output.OutputTypeArguments);
    }

    [Fact]
    public void ValueEquality_HoldsForIdenticalData()
    {
        var a = new OutputMetadata(null, null);
        var b = new OutputMetadata(null, null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueEquality_HoldsForDistinctArrayInstancesWithSameElements()
    {
        // Two distinct ImmutableArray<string> with the same type-argument values must compare equal.
        var a = new OutputMetadata("List", ImmutableArray.Create("string"));
        var b = new OutputMetadata("List", ImmutableArray.Create("string"));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueInequality_WhenTypeNameDiffers()
    {
        var a = new OutputMetadata("T1", null);
        var b = new OutputMetadata("T2", null);

        Assert.NotEqual(a, b);
    }
}

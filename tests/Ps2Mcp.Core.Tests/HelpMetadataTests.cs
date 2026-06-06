using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class HelpMetadataTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var examples = ImmutableArray.Create(new HelpExample(null, "Get-Foo -Name bar", null));

        var help = new HelpMetadata(
            Synopsis: "Gets a foo.",
            Description: "Longer description.",
            Examples: examples);

        Assert.Equal("Gets a foo.", help.Synopsis);
        Assert.Equal("Longer description.", help.Description);
        Assert.Equal(examples, help.Examples);
    }

    [Fact]
    public void AllFields_CanBeNullOrEmpty()
    {
        var help = new HelpMetadata(null, null, ImmutableArray<HelpExample>.Empty);

        Assert.Null(help.Synopsis);
        Assert.Null(help.Description);
        Assert.Empty(help.Examples);
    }

    [Fact]
    public void ValueEquality_HoldsForIdenticalData()
    {
        var a = new HelpMetadata("S", "D", ImmutableArray<HelpExample>.Empty);
        var b = new HelpMetadata("S", "D", ImmutableArray<HelpExample>.Empty);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueEquality_HoldsForDistinctArrayInstancesWithSameElements()
    {
        // Two distinct ImmutableArray<HelpExample> with element-identical contents must compare equal.
        var exampleA = new HelpExample(null, "Get-Foo -Name bar", null);
        var exampleB = new HelpExample(null, "Get-Foo -Name bar", null);
        var a = new HelpMetadata("S", "D", ImmutableArray.Create(exampleA));
        var b = new HelpMetadata("S", "D", ImmutableArray.Create(exampleB));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueInequality_WhenSynopsisDiffers()
    {
        var a = new HelpMetadata("S1", "D", ImmutableArray<HelpExample>.Empty);
        var b = new HelpMetadata("S2", "D", ImmutableArray<HelpExample>.Empty);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void HashCode_DiffersWhenOnlyExamplesSequenceContentsDiffer()
    {
        // Regression: sequence contents must contribute to GetHashCode.
        var a = new HelpMetadata("S", "D", ImmutableArray<HelpExample>.Empty);
        var b = new HelpMetadata("S", "D", ImmutableArray.Create(new HelpExample(null, "Get-Foo -Name bar", null)));

        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }
}

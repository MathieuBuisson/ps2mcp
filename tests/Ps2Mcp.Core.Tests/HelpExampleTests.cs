using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class HelpExampleTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var example = new HelpExample(Title: "Example 1", Code: "Get-Foo -Name bar", Remarks: "Common usage.");

        Assert.Equal("Example 1", example.Title);
        Assert.Equal("Get-Foo -Name bar", example.Code);
        Assert.Equal("Common usage.", example.Remarks);
    }

    [Fact]
    public void Title_And_Remarks_CanBeNull()
    {
        var example = new HelpExample(null, "Get-Foo", null);

        Assert.Null(example.Title);
        Assert.Null(example.Remarks);
    }

    [Fact]
    public void ValueEquality_HoldsForIdenticalData()
    {
        var a = new HelpExample(null, "Get-Foo", null);
        var b = new HelpExample(null, "Get-Foo", null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueInequality_WhenCodeDiffers()
    {
        var a = new HelpExample(null, "Get-Foo", null);
        var b = new HelpExample(null, "Get-Bar", null);

        Assert.NotEqual(a, b);
    }
}

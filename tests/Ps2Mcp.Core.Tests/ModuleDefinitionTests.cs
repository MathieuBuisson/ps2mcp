using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class ModuleDefinitionTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var module = new ModuleDefinition("MyModule", "2.0.0");

        Assert.Equal("MyModule", module.Name);
        Assert.Equal("2.0.0", module.Version);
    }

    [Fact]
    public void Version_CanBeNull()
    {
        var module = new ModuleDefinition("MyModule", null);

        Assert.Null(module.Version);
    }

    [Fact]
    public void ValueEquality_HoldsForIdenticalData()
    {
        var a = new ModuleDefinition("M", "1.0");
        var b = new ModuleDefinition("M", "1.0");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueInequality_WhenNameDiffers()
    {
        var a = new ModuleDefinition("A", "1.0");
        var b = new ModuleDefinition("B", "1.0");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ValueInequality_WhenVersionDiffers()
    {
        var a = new ModuleDefinition("M", "1.0");
        var b = new ModuleDefinition("M", "2.0");

        Assert.NotEqual(a, b);
    }
}

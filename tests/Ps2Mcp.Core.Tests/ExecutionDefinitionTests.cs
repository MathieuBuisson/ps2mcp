using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class ExecutionDefinitionTests
{
    [Fact]
    public void Constructor_StoresValue()
    {
        var execution = new ExecutionDefinition(SerializationDepth: 8);

        Assert.Equal(8, execution.SerializationDepth);
    }

    [Fact]
    public void ValueEquality_HoldsForIdenticalData()
    {
        var a = new ExecutionDefinition(4);
        var b = new ExecutionDefinition(4);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueInequality_WhenSerializationDepthDiffers()
    {
        var a = new ExecutionDefinition(4);
        var b = new ExecutionDefinition(8);

        Assert.NotEqual(a, b);
    }
}

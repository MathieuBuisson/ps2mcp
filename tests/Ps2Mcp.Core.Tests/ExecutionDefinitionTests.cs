using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class ExecutionDefinitionTests
{
    [Fact]
    public void Constructor_StoresValue()
    {
        var execution = new ExecutionDefinition(SerializationDepth: 8, TimeoutMs: 60000);

        Assert.Equal(8, execution.SerializationDepth);
        Assert.Equal(60000, execution.TimeoutMs);
    }

    [Fact]
    public void ValueEquality_HoldsForIdenticalData()
    {
        var a = new ExecutionDefinition(4, ExecutionDefinition.DefaultTimeoutMs);
        var b = new ExecutionDefinition(4, ExecutionDefinition.DefaultTimeoutMs);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueInequality_WhenSerializationDepthDiffers()
    {
        var a = new ExecutionDefinition(4, ExecutionDefinition.DefaultTimeoutMs);
        var b = new ExecutionDefinition(8, ExecutionDefinition.DefaultTimeoutMs);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ValueInequality_WhenTimeoutMsDiffers()
    {
        var a = new ExecutionDefinition(4, ExecutionDefinition.DefaultTimeoutMs);
        var b = new ExecutionDefinition(4, 60000);

        Assert.NotEqual(a, b);
    }
}

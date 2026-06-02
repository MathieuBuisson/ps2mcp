using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class McpServerDefinitionTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var module = new ModuleDefinition("M", "1.0");
        var tools = ImmutableArray<ToolDefinition>.Empty;

        var server = new McpServerDefinition(module, tools);

        Assert.Same(module, server.Module);
        Assert.Equal(tools, server.Tools);
    }

    [Fact]
    public void ValueEquality_HoldsForIdenticalData()
    {
        var module = new ModuleDefinition("M", "1.0");
        var tools = ImmutableArray<ToolDefinition>.Empty;
        var a = new McpServerDefinition(module, tools);
        var b = new McpServerDefinition(module, tools);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueEquality_HoldsForDistinctArrayInstancesWithSameElements()
    {
        // Two distinct ImmutableArray<ToolDefinition> with element-identical contents must compare equal; this is the fix for the IReadOnlyList reference-equality bug.
        var toolA = MakeTool();
        var toolB = MakeTool();
        var a = new McpServerDefinition(new ModuleDefinition("M", "1.0"), ImmutableArray.Create(toolA));
        var b = new McpServerDefinition(new ModuleDefinition("M", "1.0"), ImmutableArray.Create(toolB));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueInequality_WhenModuleDiffers()
    {
        var tools = ImmutableArray<ToolDefinition>.Empty;
        var a = new McpServerDefinition(new ModuleDefinition("A", null), tools);
        var b = new McpServerDefinition(new ModuleDefinition("B", null), tools);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void IrVersion_ConstantIsOne()
    {
        Assert.Equal(1, IrVersion.Current);
    }

    [Fact]
    public void IrVersion_DefaultsToCurrentVersion()
    {
        var server = new McpServerDefinition(new ModuleDefinition("M", null), ImmutableArray<ToolDefinition>.Empty);

        Assert.Equal(IrVersion.Current, server.IrVersion);
    }

    [Fact]
    public void IrVersion_CanBeOverriddenAtConstruction()
    {
        // The with-expression can be used to bump the version for migration testing or to model a future format.
        var server = new McpServerDefinition(new ModuleDefinition("M", null), ImmutableArray<ToolDefinition>.Empty) with { IrVersion = 2 };

        Assert.Equal(2, server.IrVersion);
    }

    [Fact]
    public void IrVersion_DifferentVersionsYieldInequality()
    {
        var module = new ModuleDefinition("M", null);
        var tools = ImmutableArray<ToolDefinition>.Empty;
        var a = new McpServerDefinition(module, tools) with { IrVersion = 1 };
        var b = new McpServerDefinition(module, tools) with { IrVersion = 2 };

        Assert.NotEqual(a, b);
    }

    private static ToolDefinition MakeTool() =>
        new(
            ToolName: "GetFoo",
            SourceCommand: "Get-Foo",
            Description: "Gets a foo.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null),
            Execution: new ExecutionDefinition(4),
            Help: null,
            Output: null);
}

using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class ToolDefinitionTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var parameters = ImmutableArray<ParameterDefinition>.Empty;
        var schema = new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null);
        var execution = new ExecutionDefinition(4);

        var tool = new ToolDefinition("GetFoo", "Get-Foo", "Gets a foo.", parameters, "Default", schema, execution, null, null);

        Assert.Equal("GetFoo", tool.ToolName);
        Assert.Equal("Get-Foo", tool.SourceCommand);
        Assert.Equal("Gets a foo.", tool.Description);
        Assert.Equal(parameters, tool.Parameters);
        Assert.Equal("Default", tool.RequiredParameterSet);
        Assert.Same(schema, tool.Schema);
        Assert.Same(execution, tool.Execution);
        Assert.Null(tool.Help);
        Assert.Null(tool.Output);
    }

    [Fact]
    public void RequiredParameterSet_Help_And_Output_CanBeNull()
    {
        var tool = MakeTool(null, null, null);

        Assert.Null(tool.RequiredParameterSet);
        Assert.Null(tool.Help);
        Assert.Null(tool.Output);
    }

    [Fact]
    public void ValueEquality_HoldsForIdenticalData()
    {
        var a = MakeTool("Default", null, null);
        var b = MakeTool("Default", null, null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueEquality_HoldsForDistinctArrayInstancesWithSameElements()
    {
        // Two distinct ImmutableArray<ParameterDefinition> with element-identical contents must compare equal.
        var param1 = new ParameterDefinition("Name", "string", true, false, null, null, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
        var param2 = new ParameterDefinition("Name", "string", true, false, null, null, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
        var a = new ToolDefinition("GetFoo", "Get-Foo", "d", ImmutableArray.Create(param1), null, EmptySchema(), new ExecutionDefinition(4), null, null);
        var b = new ToolDefinition("GetFoo", "Get-Foo", "d", ImmutableArray.Create(param2), null, EmptySchema(), new ExecutionDefinition(4), null, null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueInequality_WhenToolNameDiffers()
    {
        var a = MakeTool(null, null, null);
        var mutated = a with { ToolName = "OtherName" };

        Assert.NotEqual(a, mutated);
    }

    private static ToolDefinition MakeTool(string? requiredParameterSet, HelpMetadata? help, OutputMetadata? output) =>
        new(
            ToolName: "GetFoo",
            SourceCommand: "Get-Foo",
            Description: "Gets a foo.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: requiredParameterSet,
            Schema: new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null),
            Execution: new ExecutionDefinition(4),
            Help: help,
            Output: output);

    private static SchemaDefinition EmptySchema() =>
        new("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null);
}

using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Tests.Shared;

internal static class SchemaCoverageServerFixture
{
    public static McpServerDefinition Create() =>
        new(
            new ModuleDefinition(Name: "SchemaCoverage.Module", Version: "1.0.0"),
            ImmutableArray.Create(
                CreateEmptyObjectTool(),
                CreatePrimitiveTypesTool(),
                CreateIntegerEnumTool(),
                CreateNumberEnumTool(),
                CreateStringWithPatternTool(),
                CreateStringEnumWithPatternTool(),
                CreateNumericConstraintsTool(),
                CreateNestedObjectTool(),
                CreateArrayOfStringsTool(),
                CreateArrayFallbackTool(),
                CreateUnknownTypeTool(),
                CreateRequiredOptionalTool()));

    private static ToolDefinition CreateTool(string name, SchemaDefinition schema) =>
        new(
            ToolName: name,
            SourceCommand: $"Invoke-{name}",
            Description: $"Tests {name} schema.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: schema,
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);

    private static ToolDefinition CreateEmptyObjectTool() =>
        CreateTool(
            "empty_object",
            new SchemaDefinition("object", [], [], null));

    private static ToolDefinition CreatePrimitiveTypesTool() =>
        CreateTool(
            "primitive_types",
            new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray.Create(
                    new SchemaProperty(Name: "IntVal", Type: "integer", Enum: null, Minimum: null, Maximum: null, Pattern: null, Schema: null),
                    new SchemaProperty(Name: "NumVal", Type: "number", Enum: null, Minimum: null, Maximum: null, Pattern: null, Schema: null),
                    new SchemaProperty(Name: "BoolVal", Type: "boolean", Enum: null, Minimum: null, Maximum: null, Pattern: null, Schema: null),
                    new SchemaProperty(Name: "StrVal", Type: "string", Enum: null, Minimum: null, Maximum: null, Pattern: null, Schema: null)),
                Required: ImmutableArray.Create("IntVal", "NumVal", "BoolVal", "StrVal"),
                Items: null));

    private static ToolDefinition CreateIntegerEnumTool() =>
        CreateTool(
            "integer_enum",
            new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray.Create(
                    new SchemaProperty(Name: "Priority", Type: "integer", Enum: ImmutableArray.Create("1", "2", "3"), Minimum: null, Maximum: null, Pattern: null, Schema: null),
                    new SchemaProperty(Name: "PriorityBounded", Type: "integer", Enum: ImmutableArray.Create("1", "2", "3"), Minimum: "1", Maximum: "3", Pattern: null, Schema: null)),
                Required: ImmutableArray<string>.Empty,
                Items: null));

    private static ToolDefinition CreateNumberEnumTool() =>
        CreateTool(
            "number_enum",
            new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray.Create(
                    new SchemaProperty(Name: "Score", Type: "number", Enum: ImmutableArray.Create("1.5", "2.5"), Minimum: null, Maximum: null, Pattern: null, Schema: null),
                    new SchemaProperty(Name: "ScoreBounded", Type: "number", Enum: ImmutableArray.Create("1.5", "2.5"), Minimum: "1.0", Maximum: "3.0", Pattern: null, Schema: null)),
                Required: ImmutableArray<string>.Empty,
                Items: null));

    private static ToolDefinition CreateStringWithPatternTool() =>
        CreateTool(
            "string_pattern",
            new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray.Create(
                    new SchemaProperty(Name: "Email", Type: "string", Enum: null, Minimum: null, Maximum: null, Pattern: "^.+@.+\\..+$", Schema: null)),
                Required: ImmutableArray<string>.Empty,
                Items: null));

    private static ToolDefinition CreateStringEnumWithPatternTool() =>
        CreateTool(
            "string_enum_pattern",
            new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray.Create(
                    new SchemaProperty(Name: "Mode", Type: "string", Enum: ImmutableArray.Create("Alpha", "Beta"), Minimum: null, Maximum: null, Pattern: "^[A-Z][a-z]+$", Schema: null)),
                Required: ImmutableArray<string>.Empty,
                Items: null));

    private static ToolDefinition CreateNumericConstraintsTool() =>
        CreateTool(
            "numeric_constraints",
            new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray.Create(
                    new SchemaProperty(Name: "Count", Type: "integer", Enum: null, Minimum: "1", Maximum: "100", Pattern: null, Schema: null)),
                Required: ImmutableArray.Create("Count"),
                Items: null));

    private static ToolDefinition CreateNestedObjectTool() =>
        CreateTool(
            "nested_object",
            new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray.Create(
                    new SchemaProperty(
                        Name: "Config",
                        Type: "object",
                        Enum: null,
                        Minimum: null,
                        Maximum: null,
                        Pattern: null,
                        Schema: new SchemaDefinition(
                            Type: "object",
                            Properties: ImmutableArray.Create(
                                new SchemaProperty(Name: "Id", Type: "integer", Enum: null, Minimum: null, Maximum: null, Pattern: null, Schema: null)),
                            Required: ImmutableArray.Create("Id"),
                            Items: null))),
                Required: ImmutableArray.Create("Config"),
                Items: null));

    private static ToolDefinition CreateArrayOfStringsTool() =>
        CreateTool(
            "array_of_strings",
            new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray.Create(
                    new SchemaProperty(
                        Name: "Tags",
                        Type: "array",
                        Enum: null,
                        Minimum: null,
                        Maximum: null,
                        Pattern: null,
                        Schema: new SchemaDefinition(Type: "string", Properties: [], Required: [], Items: null))),
                Required: ImmutableArray.Create("Tags"),
                Items: null));

    private static ToolDefinition CreateArrayFallbackTool() =>
        CreateTool(
            "array_fallback",
            new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray.Create(
                    new SchemaProperty(
                        Name: "MysteryValues",
                        Type: "array",
                        Enum: null,
                        Minimum: null,
                        Maximum: null,
                        Pattern: null,
                        Schema: new SchemaDefinition(Type: "array", Properties: [], Required: [], Items: null))),
                Required: ImmutableArray<string>.Empty,
                Items: null));

    private static ToolDefinition CreateUnknownTypeTool() =>
        CreateTool(
            "unknown_type",
            new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray.Create(
                    new SchemaProperty(Name: "Mystery", Type: "custom_type", Enum: null, Minimum: null, Maximum: null, Pattern: null, Schema: null)),
                Required: ImmutableArray<string>.Empty,
                Items: null));

    private static ToolDefinition CreateRequiredOptionalTool() =>
        CreateTool(
            "required_optional",
            new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray.Create(
                    new SchemaProperty(Name: "Required", Type: "string", Enum: null, Minimum: null, Maximum: null, Pattern: null, Schema: null),
                    new SchemaProperty(Name: "Optional", Type: "string", Enum: null, Minimum: null, Maximum: null, Pattern: null, Schema: null)),
                Required: ImmutableArray.Create("Required"),
                Items: null));
}

using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Tests.Shared;

internal static class RepresentativeServerFixture
{
    private const string DefaultParameterSet = "Default";

    public static McpServerDefinition Create() =>
        new(CreateModule(), ImmutableArray.Create(CreateGetDemoItemTool()));

    private static ModuleDefinition CreateModule() =>
        new(Name: "Demo.Module", Version: "1.2.3");

    private static ToolDefinition CreateGetDemoItemTool() =>
        new(
            ToolName: "get_demo_item",
            SourceCommand: "Get-DemoItem",
            Description: "Gets a demo item.",
            Parameters: ImmutableArray.Create(CreateNameParameter(), CreateSecretParameter()),
            RequiredParameterSet: DefaultParameterSet,
            Schema: CreateItemSchema(),
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth),
            Help: new HelpMetadata(
                Synopsis: "Gets demo data.",
                Description: "Returns representative data used by emitter fixture tests.",
                Examples: ImmutableArray.Create(
                    new HelpExample(
                        Title: "Basic usage",
                        Code: "Get-DemoItem -Name Widget",
                        Remarks: null))),
            Output: new OutputMetadata(
                OutputTypeName: "Demo.Item",
                OutputTypeArguments: ImmutableArray.Create("string")));

    private static ParameterDefinition CreateNameParameter() =>
        new(
            Name: "Name",
            Type: "string",
            IsMandatory: true,
            IsSecure: false,
            Description: "The item name.",
            DefaultValue: null,
            Aliases: ImmutableArray.Create("ItemName"),
            ParameterSets: ImmutableArray.Create(DefaultParameterSet));

    private static ParameterDefinition CreateSecretParameter() =>
        new(
            Name: "Secret",
            Type: "SecureString",
            IsMandatory: false,
            IsSecure: true,
            Description: "A secret token.",
            DefaultValue: null,
            Aliases: [],
            ParameterSets: ImmutableArray.Create(DefaultParameterSet));

    private static SchemaDefinition CreateItemSchema() =>
        new(
            Type: "object",
            Properties: ImmutableArray.Create(CreateNameSchemaProperty(), CreateTagsSchemaProperty()),
            Required: ImmutableArray.Create("Name"),
            Items: null);

    private static SchemaProperty CreateNameSchemaProperty() =>
        new(
            Name: "Name",
            Type: "string",
            Enum: null,
            Minimum: null,
            Maximum: null,
            Pattern: null,
            Schema: null);

    private static SchemaProperty CreateTagsSchemaProperty() =>
        new(
            Name: "Tags",
            Type: "array",
            Enum: null,
            Minimum: null,
            Maximum: null,
            Pattern: null,
            Schema: new SchemaDefinition(
                Type: "string",
                Properties: [],
                Required: [],
                Items: null));
}

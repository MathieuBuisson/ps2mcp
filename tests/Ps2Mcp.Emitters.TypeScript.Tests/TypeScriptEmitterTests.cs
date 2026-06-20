using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ps2Mcp.Core;
using Ps2Mcp.Emitters.TypeScript;
using Ps2Mcp.Tests.Shared;

namespace Ps2Mcp.Emitters.TypeScript.Tests;

public sealed class TypeScriptEmitterTests
{
    [Fact]
    public void RepresentativeFixture_TagsProperty_IsArrayOfStrings_Assumption()
    {
        var server = RepresentativeServerFixture.Create();

        var tags = server.Tools[0].Schema.Properties.Single(property => property.Name == "Tags");

        Assert.Equal("array", tags.Type);
        Assert.NotNull(tags.Schema);
        Assert.Equal("string", tags.Schema!.Type);
        Assert.Null(tags.Schema.Items);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_ReturnsIndexTsSnapshot()
    {
        var emitter = new TypeScriptEmitter();
        var server = RepresentativeServerFixture.Create();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");

        var result = await emitter.EmitAsync(server, options);
        var file = Assert.Single(result.Files);

        Assert.Equal("src/index.ts", file.RelativePath);
        Assert.Equal(ReadEmbeddedText("Ps2Mcp.Emitters.TypeScript.Tests.Snapshots.RepresentativeIndex.ts"), file.Contents);
    }

    [Fact]
    public async Task EmitAsync_NullServer_ThrowsArgumentNullException()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => emitter.EmitAsync(null!, options));

        Assert.Equal("server", ex.ParamName);
    }

    [Fact]
    public async Task EmitAsync_NullOptions_ThrowsArgumentNullException()
    {
        var emitter = new TypeScriptEmitter();
        var server = RepresentativeServerFixture.Create();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => emitter.EmitAsync(server, null!));

        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public async Task EmitAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var emitter = new TypeScriptEmitter();
        var server = RepresentativeServerFixture.Create();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => emitter.EmitAsync(server, options, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task EmitAsync_ToolNamesThatNormalizeToSameIdentifier_UseUniqueSchemaConstNames()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var sharedSchema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray<SchemaProperty>.Empty,
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var toolA = new ToolDefinition(
            ToolName: "get-demo",
            SourceCommand: "Get-Demo",
            Description: "First tool.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: sharedSchema,
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth),
            Help: null,
            Output: null);
        var toolB = new ToolDefinition(
            ToolName: "get_demo",
            SourceCommand: "Get-DemoAlt",
            Description: "Second tool.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: sharedSchema,
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(toolA, toolB));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains("const getDemoInputSchema = z.object({", contents, StringComparison.Ordinal);
        Assert.Contains("const getDemoInputSchema2 = z.object({", contents, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(contents, "const getDemoInputSchema", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmitAsync_AllCapsToolName_PreservesSegmentCasing()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray<SchemaProperty>.Empty,
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var tool = new ToolDefinition(
            ToolName: "GET-FOO",
            SourceCommand: "Get-Foo",
            Description: "Gets foo.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: schema,
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains("const getFooInputSchema = z.object({", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_ArraySchemaWithoutItems_FallsBackToUnknownElementType()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "MysteryValues",
                    Type: "array",
                    Enum: null,
                    Minimum: null,
                    Maximum: null,
                    Pattern: null,
                    Schema: new SchemaDefinition(
                        Type: "array",
                        Properties: ImmutableArray<SchemaProperty>.Empty,
                        Required: ImmutableArray<string>.Empty,
                        Items: null))),
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var tool = new ToolDefinition(
            ToolName: "get_demo_item",
            SourceCommand: "Get-DemoItem",
            Description: "Gets a demo item.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: schema,
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains("MysteryValues: z.array(z.unknown()).optional()", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_StringEnumWithPattern_UsesRefineInsteadOfRegexOnEnum()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Mode",
                    Type: "string",
                    Enum: ImmutableArray.Create("Alpha", "Beta"),
                    Minimum: null,
                    Maximum: null,
                    Pattern: "^[A-Z][a-z]+$",
                    Schema: null)),
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var tool = new ToolDefinition(
            ToolName: "get_demo_item",
            SourceCommand: "Get-DemoItem",
            Description: "Gets a demo item.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: schema,
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains("Mode: z.enum([\"Alpha\", \"Beta\"]).refine((value) => new RegExp(\"^[A-Z][a-z]+$\").test(value)", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("Mode: z.enum([\"Alpha\", \"Beta\"]).regex(", contents, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("integer", "z.number().int()")]
    [InlineData("number", "z.number()")]
    [InlineData("boolean", "z.boolean()")]
    public async Task EmitAsync_NumericAndBooleanTypes_RenderCorrectZodType(string type, string expectedExpression)
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Value",
                    Type: type,
                    Enum: null,
                    Minimum: null,
                    Maximum: null,
                    Pattern: null,
                    Schema: null)),
            Required: ImmutableArray.Create("Value"),
            Items: null);
        var tool = CreateTool(schema);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains($"Value: {expectedExpression}", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_StringWithPattern_RendersRegex()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Email",
                    Type: "string",
                    Enum: null,
                    Minimum: null,
                    Maximum: null,
                    Pattern: "^.+@.+\\..+$",
                    Schema: null)),
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var tool = CreateTool(schema);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains("Email: z.string().regex(new RegExp(\"^.+@.+\\\\..+$\"))", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_NumericConstraints_RendersMinMax()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Count",
                    Type: "integer",
                    Enum: null,
                    Minimum: "1",
                    Maximum: "100",
                    Pattern: null,
                    Schema: null)),
            Required: ImmutableArray.Create("Count"),
            Items: null);
        var tool = CreateTool(schema);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains("Count: z.number().int().min(1).max(100)", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_RequiredProperty_OmitsOptional()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Required",
                    Type: "string",
                    Enum: null,
                    Minimum: null,
                    Maximum: null,
                    Pattern: null,
                    Schema: null),
                new SchemaProperty(
                    Name: "Optional",
                    Type: "string",
                    Enum: null,
                    Minimum: null,
                    Maximum: null,
                    Pattern: null,
                    Schema: null)),
            Required: ImmutableArray.Create("Required"),
            Items: null);
        var tool = CreateTool(schema);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains("Required: z.string(),", contents, StringComparison.Ordinal);
        Assert.Contains("Optional: z.string().optional(),", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_NestedObjectProperty_RendersMultiLine()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var innerSchema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Id",
                    Type: "integer",
                    Enum: null,
                    Minimum: null,
                    Maximum: null,
                    Pattern: null,
                    Schema: null)),
            Required: ImmutableArray.Create("Id"),
            Items: null);
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Config",
                    Type: "object",
                    Enum: null,
                    Minimum: null,
                    Maximum: null,
                    Pattern: null,
                    Schema: innerSchema)),
            Required: ImmutableArray.Create("Config"),
            Items: null);
        var tool = CreateTool(schema);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains("Config: z.object({", contents, StringComparison.Ordinal);
        Assert.Contains("Id: z.number().int()", contents, StringComparison.Ordinal);
        Assert.Contains("})", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_EmptyObjectSchema_RendersEmptyObjectBraces()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray<SchemaProperty>.Empty,
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var tool = CreateTool(schema);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains("z.object({", contents, StringComparison.Ordinal);
        Assert.Contains("});", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_UnknownType_RendersZodUnknown()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Mystery",
                    Type: "custom_type",
                    Enum: null,
                    Minimum: null,
                    Maximum: null,
                    Pattern: null,
                    Schema: null)),
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var tool = CreateTool(schema);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains("Mystery: z.unknown().optional()", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_EmptyToolDescription_EmitsEmptyString()
    {
        var emitter = new TypeScriptEmitter();
        var options = new EmitOptions("./modules/Demo.Module/Demo.Module.psd1");
        var tool = new ToolDefinition(
            ToolName: "empty_desc_tool",
            SourceCommand: "Get-EmptyDesc",
            Description: "",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: new SchemaDefinition("object", [], [], null),
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = Assert.Single(result.Files).Contents;

        Assert.Contains("description: \"\",", contents, StringComparison.Ordinal);
    }

    private static ToolDefinition CreateTool(SchemaDefinition schema) =>
        new(
            ToolName: "test_tool",
            SourceCommand: "Get-Test",
            Description: "Test tool.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: schema,
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth),
            Help: null,
            Output: null);

    private static string ReadEmbeddedText(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value, StringComparison comparison)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, comparison)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

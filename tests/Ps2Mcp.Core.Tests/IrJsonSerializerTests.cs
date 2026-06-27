using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;
using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class IrJsonSerializerTests
{
    [Fact]
    public void Serialize_ReturnsNonEmptyBytes()
    {
        var server = MakeServer();

        var bytes = IrJsonSerializer.Serialize(server);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var server = MakeServer();

        var bytes = IrJsonSerializer.Serialize(server);

        using var doc = JsonDocument.Parse(bytes);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void Serialize_RootPropertiesAppearInDeclarationOrder()
    {
        // McpServerDefinition primary constructor is (ModuleDefinition Module, ImmutableArray<ToolDefinition> Tools, int IrVersion).
        // The source generator must emit properties in that order so manifest.json diffs are stable.
        var server = MakeServer();

        var bytes = IrJsonSerializer.Serialize(server);
        using var doc = JsonDocument.Parse(bytes);

        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "Module", "Tools", "IrVersion" }, names);
    }

    [Fact]
    public void Serialize_EmitsIndentedJson()
    {
        var server = MakeServer();

        var bytes = IrJsonSerializer.Serialize(server);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Contains("\n", text);
        Assert.Contains("  ", text);
    }

    [Fact]
    public void Serialize_EmitsLfLineEndingsOnly()
    {
        // Explicit invariant: NewLine = "\n" must hold on every OS so Windows and Linux produce the same bytes.
        var server = MakeServer();

        var bytes = IrJsonSerializer.Serialize(server);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain("\r", text);
    }

    [Fact]
    public void Serialize_ByteIdenticalAcrossRepeatedInvocations()
    {
        var server = MakeServer();

        var first = IrJsonSerializer.Serialize(server);
        var second = IrJsonSerializer.Serialize(server);
        var third = IrJsonSerializer.Serialize(server);

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    [Fact]
    public void Serialize_ByteIdenticalForTwoFreshlyConstructedIdenticalServers()
    {
        var a = MakeServer();
        var b = MakeServer();

        Assert.Equal(IrJsonSerializer.Serialize(a), IrJsonSerializer.Serialize(b));
    }

    [Fact]
    public void Serialize_ByteIdenticalForDistinctImmutableArrayInstancesWithSameElements()
    {
        // Regression guard: a serializer that depended on collection instance identity, iteration order nondeterminism,
        // or a per-call cache would produce different bytes for two ImmutableArray<T> with element-identical contents.
        var s1 = new McpServerDefinition(MakeModule(), ImmutableArray.Create(MakeTool()));
        var s2 = new McpServerDefinition(MakeModule(), ImmutableArray.Create(MakeTool()));

        Assert.Equal(IrJsonSerializer.Serialize(s1), IrJsonSerializer.Serialize(s2));
    }

    [Fact]
    public void Serialize_StreamOverloadProducesSameBytesAsBytesOverload()
    {
        var server = MakeServer();

        var bytes = IrJsonSerializer.Serialize(server);
        using var stream = new MemoryStream();
        IrJsonSerializer.Serialize(server, stream);

        Assert.Equal(bytes, stream.ToArray());
    }

    [Fact]
    public void Serialize_RoundTripsToEqualValue()
    {
        var original = MakeServer();

        var bytes = IrJsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize(bytes, IrJsonSerializerContext.Default.McpServerDefinition);

        Assert.NotNull(roundTripped);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Serialize_EmptyServerIsStable()
    {
        var server = new McpServerDefinition(new ModuleDefinition("M", null), ImmutableArray<ToolDefinition>.Empty);

        var bytes = IrJsonSerializer.Serialize(server);
        using var doc = JsonDocument.Parse(bytes);

        var tools = doc.RootElement.GetProperty("Tools");
        Assert.Equal(JsonValueKind.Array, tools.ValueKind);
        Assert.Equal(0, tools.GetArrayLength());
    }

    [Fact]
    public void Serialize_NullableFieldsBecomeJsonNull()
    {
        // ToolDefinition has three nullable reference fields; all must serialize as JSON null, not be omitted.
        var tool = new ToolDefinition(
            ToolName: "T",
            SourceCommand: "T",
            Description: "D",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null),
            Execution: new ExecutionDefinition(4, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(new ModuleDefinition("M", null), ImmutableArray.Create(tool));

        var bytes = IrJsonSerializer.Serialize(server);
        using var doc = JsonDocument.Parse(bytes);
        var t = doc.RootElement.GetProperty("Tools")[0];

        Assert.Equal(JsonValueKind.Null, t.GetProperty("RequiredParameterSet").ValueKind);
        Assert.Equal(JsonValueKind.Null, t.GetProperty("Help").ValueKind);
        Assert.Equal(JsonValueKind.Null, t.GetProperty("Output").ValueKind);
    }

    [Fact]
    public void Serialize_NestedTypesAreIncludedAsObjects()
    {
        var server = MakeServer();

        var bytes = IrJsonSerializer.Serialize(server);
        using var doc = JsonDocument.Parse(bytes);
        var tool = doc.RootElement.GetProperty("Tools")[0];

        Assert.Equal(JsonValueKind.Object, tool.GetProperty("Schema").ValueKind);
        Assert.Equal(JsonValueKind.Object, tool.GetProperty("Help").ValueKind);
        Assert.Equal(JsonValueKind.Object, tool.GetProperty("Output").ValueKind);
    }

    [Fact]
    public void Serialize_EmitsIrVersionAsInteger()
    {
        var server = MakeServer();

        var bytes = IrJsonSerializer.Serialize(server);
        using var doc = JsonDocument.Parse(bytes);
        var ir = doc.RootElement.GetProperty("IrVersion");

        Assert.Equal(JsonValueKind.Number, ir.ValueKind);
        Assert.Equal(IrVersion.Current, ir.GetInt32());
    }

    [Fact]
    public void Serialize_EmitsIntegerDepthWithoutLocaleFormatting()
    {
        // System.Text.Json writes integers invariantly. A culture-aware regression would emit
        // "12.345" or "12,345" on non-English locales; canonical invariant form for 4 is "4".
        var server = new McpServerDefinition(
            new ModuleDefinition("M", null),
            ImmutableArray.Create(MakeTool()));

        var bytes = IrJsonSerializer.Serialize(server);
        using var doc = JsonDocument.Parse(bytes);
        var depth = doc.RootElement.GetProperty("Tools")[0].GetProperty("Execution").GetProperty("SerializationDepth");

        Assert.Equal(JsonValueKind.Number, depth.ValueKind);
        Assert.Equal(4, depth.GetInt32());
    }

    private static McpServerDefinition MakeServer() =>
        new(
            Module: new ModuleDefinition("M", "1.0"),
            Tools: ImmutableArray.Create(MakeTool()));

    private static ModuleDefinition MakeModule() => new("M", "1.0");

    private static ToolDefinition MakeTool() =>
        new(
            ToolName: "GetFoo",
            SourceCommand: "Get-Foo",
            Description: "Gets a foo.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null),
            Execution: new ExecutionDefinition(4, ExecutionDefinition.DefaultTimeoutMs),
            Help: new HelpMetadata("Gets a foo.", "Longer description.", ImmutableArray<HelpExample>.Empty),
            Output: new OutputMetadata("string", null));
}

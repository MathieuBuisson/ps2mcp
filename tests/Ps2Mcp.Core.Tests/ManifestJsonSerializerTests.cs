using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Ps2Mcp.Core.Tests;

public sealed class ManifestJsonSerializerTests
{
    [Fact]
    public void Deserialize_StreamOverload_NullSource_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ManifestJsonSerializer.Deserialize((Stream)null!));
        Assert.Equal("utf8Json", ex.ParamName);
    }

    [Fact]
    public void Serialize_NullManifest_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ManifestJsonSerializer.Serialize(null!));
        Assert.Equal("manifest", ex.ParamName);
    }

    [Fact]
    public void Serialize_StreamOverload_NullManifest_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();
        var ex = Assert.Throws<ArgumentNullException>(() => ManifestJsonSerializer.Serialize(null!, stream));
        Assert.Equal("manifest", ex.ParamName);
    }

    [Fact]
    public void Serialize_StreamOverload_NullDestination_ThrowsArgumentNullException()
    {
        var manifest = ManifestFixtures.MakeDefault();
        var ex = Assert.Throws<ArgumentNullException>(() => ManifestJsonSerializer.Serialize(manifest, null!));
        Assert.Equal("destination", ex.ParamName);
    }

    [Fact]
    public void Serialize_ReturnsNonEmptyBytes()
    {
        var manifest = ManifestFixtures.MakeDefault();

        var bytes = ManifestJsonSerializer.Serialize(manifest);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var manifest = ManifestFixtures.MakeDefault();

        var bytes = ManifestJsonSerializer.Serialize(manifest);

        using var doc = JsonDocument.Parse(bytes);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    // Contract: manifest.json property order is enforced by [JsonPropertyOrder] on all serialized records.
    [Fact]
    public void Serialize_RootPropertiesAppearInJsonContractOrder()
    {
        var manifest = ManifestFixtures.MakeDefault();

        var bytes = ManifestJsonSerializer.Serialize(manifest);
        using var doc = JsonDocument.Parse(bytes);

        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(ManifestFixtures.GetJsonPropertyOrder<ManifestDefinition>(), names);
    }

    [Fact]
    public void Serialize_EmitsIndentedJsonWithLfOnly()
    {
        var manifest = ManifestFixtures.MakeDefault();

        var bytes = ManifestJsonSerializer.Serialize(manifest);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Contains("\n", text);
        Assert.Contains("  ", text);
        Assert.DoesNotContain("\r", text);
    }

    [Fact]
    public void Serialize_StreamOverloadProducesSameBytesAsBytesOverload()
    {
        var manifest = ManifestFixtures.MakeDefault();

        var bytes = ManifestJsonSerializer.Serialize(manifest);
        using var stream = new MemoryStream();
        ManifestJsonSerializer.Serialize(manifest, stream);

        Assert.Equal(bytes, stream.ToArray());
    }

    [Fact]
    public async Task SerializeAsync_ProducesSameBytesAsSyncOverload()
    {
        var manifest = ManifestFixtures.MakeDefault();

        var bytes = ManifestJsonSerializer.Serialize(manifest);
        using var stream = new MemoryStream();
        await ManifestJsonSerializer.SerializeAsync(manifest, stream);

        Assert.Equal(bytes, stream.ToArray());
    }

    [Fact]
    public async Task SerializeAsync_NullManifest_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<ArgumentNullException>(() => ManifestJsonSerializer.SerializeAsync(null!, stream));
    }

    [Fact]
    public async Task SerializeAsync_NullDestination_ThrowsArgumentNullException()
    {
        var manifest = ManifestFixtures.MakeDefault();
        await Assert.ThrowsAsync<ArgumentNullException>(() => ManifestJsonSerializer.SerializeAsync(manifest, null!));
    }

    [Fact]
    public void Serialize_RoundTripsToEqualValue()
    {
        var original = ManifestFixtures.MakeDefault();

        var bytes = ManifestJsonSerializer.Serialize(original);
        var roundTripped = ManifestJsonSerializer.Deserialize(bytes);

        Assert.NotNull(roundTripped);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Deserialize_StreamOverload_RoundTripsToEqualValue()
    {
        var original = ManifestFixtures.MakeDefault();

        var bytes = ManifestJsonSerializer.Serialize(original);
        using var stream = new MemoryStream(bytes);
        var roundTripped = ManifestJsonSerializer.Deserialize(stream);

        Assert.NotNull(roundTripped);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Serialize_PreservesSchemaAndContentHash()
    {
        var manifest = ManifestFixtures.MakeDefault();

        var bytes = ManifestJsonSerializer.Serialize(manifest);
        using var doc = JsonDocument.Parse(bytes);
        var tool = doc.RootElement.GetProperty("Tools")[0];
        var parameter = tool.GetProperty("Parameters")[0];
        var schemaProperty = tool.GetProperty("Schema").GetProperty("Properties")[0];

        Assert.Equal("sha256:abc123", doc.RootElement.GetProperty("ContentHash").GetString());
        Assert.Equal("Get-Foo", tool.GetProperty("SourceCommand").GetString());
        Assert.True(parameter.GetProperty("IsSecure").GetBoolean());
        Assert.Equal("Password", schemaProperty.GetProperty("Name").GetString());
        Assert.Equal("string", schemaProperty.GetProperty("Type").GetString());
    }

    [Fact]
    public void FromServer_ProjectsSchemaRelevantFieldsOnly()
    {
        var server = new McpServerDefinition(
            Module: new ModuleDefinition("MyModule", "1.2.3"),
            Tools: ImmutableArray.Create(
                new ToolDefinition(
                    ToolName: "GetFoo",
                    SourceCommand: "Get-Foo",
                    Description: "Gets a foo.",
                    Parameters: ImmutableArray.Create(
                        new ParameterDefinition(
                            Name: "Credential",
                            Type: "PSCredential",
                            IsMandatory: true,
                            IsSecure: true,
                            Description: "Credential to use.",
                            DefaultValue: "$script:defaultCred",
                            Aliases: ImmutableArray.Create("Cred"),
                            ParameterSets: ImmutableArray.Create("Default"))),
                    RequiredParameterSet: "Default",
                    Schema: new SchemaDefinition(
                        Type: "object",
                        Properties: ImmutableArray.Create(
                            new SchemaProperty(
                                Name: "Credential",
                                Type: "object",
                                Enum: null,
                                Minimum: null,
                                Maximum: null,
                                Pattern: null,
                                Schema: new SchemaDefinition(
                                    Type: "object",
                                    Properties: ImmutableArray<SchemaProperty>.Empty,
                                    Required: ImmutableArray<string>.Empty,
                                    Items: null,
                                    ComplexType: "PSCredential"))),
                        Required: ImmutableArray.Create("Credential"),
                        Items: null),
                    Execution: new ExecutionDefinition(12),
                    Help: new HelpMetadata("Gets a foo.", "Longer description.", ImmutableArray<HelpExample>.Empty),
                    Output: new OutputMetadata("FooResult", null))),
            IrVersion: 7);

        var manifest = ManifestDefinition.FromServer(server, "sha256:def456");
        var tool = Assert.Single(manifest.Tools);
        var parameter = Assert.Single(tool.Parameters);

        Assert.Equal(server.Module, manifest.Module);
        Assert.Equal(7, manifest.IrVersion);
        Assert.Equal("sha256:def456", manifest.ContentHash);
        Assert.Equal("GetFoo", tool.ToolName);
        Assert.Equal("Get-Foo", tool.SourceCommand);
        Assert.Equal("Default", tool.RequiredParameterSet);
        Assert.Equal(server.Tools[0].Schema, tool.Schema);
        Assert.Equal("Credential", parameter.Name);
        Assert.Equal("PSCredential", parameter.Type);
        Assert.True(parameter.IsMandatory);
        Assert.True(parameter.IsSecure);
        Assert.Equal(new[] { "Cred" }, parameter.Aliases);
        Assert.Equal(new[] { "Default" }, parameter.ParameterSets);
    }

    [Fact]
    public void FromServer_DefaultToolsArray_ProducesEmptyManifestTools()
    {
        var server = new McpServerDefinition(
            Module: new ModuleDefinition("Empty", null),
            Tools: default);

        var manifest = ManifestDefinition.FromServer(server, "sha256:abc");

        Assert.Empty(manifest.Tools);
    }

    [Fact]
    public void FromTool_DefaultParametersArray_ProducesEmptyManifestParameters()
    {
        var tool = new ToolDefinition(
            ToolName: "GetFoo",
            SourceCommand: "Get-Foo",
            Description: "",
            Parameters: default,
            RequiredParameterSet: null,
            Schema: new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null),
            Execution: new ExecutionDefinition(4),
            Help: null,
            Output: null);

        var manifestTool = ManifestToolDefinition.FromTool(tool);

        Assert.Empty(manifestTool.Parameters);
    }
}

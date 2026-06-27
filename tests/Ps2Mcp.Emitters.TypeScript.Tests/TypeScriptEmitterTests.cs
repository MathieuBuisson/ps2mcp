using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
        var result = await EmitRepresentativeAsync();
        var files = result.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);

        Assert.Equal(3, files.Count);
        Assert.Equal(ReadEmbeddedText("Ps2Mcp.Emitters.TypeScript.Tests.Snapshots.RepresentativeIndex.ts"), files["src/index.ts"].Contents);
        Assert.Equal(ReadEmbeddedText("Ps2Mcp.Emitters.TypeScript.Tests.Snapshots.RepresentativePackage.json"), files["package.json"].Contents);
        Assert.Equal(ReadEmbeddedText("Ps2Mcp.Emitters.TypeScript.Tests.Snapshots.RepresentativeTsconfig.json"), files["tsconfig.json"].Contents);
        Assert.Empty(result.Files.Where(file => file.RelativePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task EmitAsync_PackageJson_UsesTsxToRunTypeScriptEntrypoint()
    {
        var result = await EmitRepresentativeAsync();
        var packageJson = GetFileContents(result, "package.json");

        Assert.Contains("\"start\": \"tsx src/index.ts\"", packageJson, StringComparison.Ordinal);
        Assert.Contains("\"tsx\": \"^4.0.0\"", packageJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"start\": \"node src/index.ts\"", packageJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_UsesPwshSpawnFlags()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains("spawn(", indexTs, StringComparison.Ordinal);
        Assert.Contains("\"pwsh\"", indexTs, StringComparison.Ordinal);
        Assert.Contains("\"-NoProfile\"", indexTs, StringComparison.Ordinal);
        Assert.Contains("\"-NonInteractive\"", indexTs, StringComparison.Ordinal);
        Assert.Contains("\"-Command\"", indexTs, StringComparison.Ordinal);
        Assert.Contains("\"Import-Module -Force $modulePath\"", indexTs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_DriverScriptContainsRequiredStatements()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains("$ErrorActionPreference = 'Stop'", indexTs, StringComparison.Ordinal);
        Assert.Contains("$modulePath = $env:PS2MCP_MODULE_PATH", indexTs, StringComparison.Ordinal);
        Assert.Contains("$sourceCommand = $env:PS2MCP_SOURCE_COMMAND", indexTs, StringComparison.Ordinal);
        Assert.Contains("$serializationDepth = [int]$env:PS2MCP_SERIALIZATION_DEPTH", indexTs, StringComparison.Ordinal);
        Assert.Contains("$argumentsJson = [Console]::In.ReadToEnd()", indexTs, StringComparison.Ordinal);
        Assert.Contains("Import-Module -Force $modulePath", indexTs, StringComparison.Ordinal);
        Assert.Contains("$result = & $sourceCommand @arguments", indexTs, StringComparison.Ordinal);
        Assert.Contains("$result | ConvertTo-Json -Depth $serializationDepth -Compress", indexTs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_DriverScriptJoinsWithSemicolons()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains("].join(\"; \");", indexTs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_TransmitsArgumentsViaStandardInput()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains("\"$argumentsJson = [Console]::In.ReadToEnd()\"", indexTs, StringComparison.Ordinal);
        Assert.Contains("child.stdin.end(argsJson, \"utf8\");", indexTs, StringComparison.Ordinal);
        Assert.Contains("stdio: [\"pipe\", \"pipe\", \"pipe\"]", indexTs, StringComparison.Ordinal);
        Assert.DoesNotContain("PS2MCP_ARGS_JSON", indexTs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_HasTimeoutConstant()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains("const runtimeDirectory = dirname(fileURLToPath(import.meta.url));", indexTs, StringComparison.Ordinal);
        Assert.Contains("const bundledModuleImportPath = resolve(runtimeDirectory, \"./modules/Demo.Module/Demo.Module.psd1\");", indexTs, StringComparison.Ordinal);
        Assert.Contains("PS2MCP_MODULE_PATH: bundledModuleImportPath", indexTs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_InvalidModuleVersion_FallsBackToDefaultVersionInPackageAndServerMetadata()
    {
        var emitter = new TypeScriptEmitter();
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "not-a-semver"),
            RepresentativeServerFixture.Create().Tools);
        var options = CreateDefaultOptions();

        var result = await emitter.EmitAsync(server, options);
        var packageJson = GetFileContents(result, "package.json");
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains("\"version\": \"0.0.0\"", packageJson, StringComparison.Ordinal);
        Assert.Contains("version: \"0.0.0\"", indexTs, StringComparison.Ordinal);
        Assert.DoesNotContain("not-a-semver", packageJson, StringComparison.Ordinal);
        Assert.DoesNotContain("not-a-semver", indexTs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_PrereleaseVersionWithInternalHyphen_PreservesVersionInPackageAndServerMetadata()
    {
        var emitter = new TypeScriptEmitter();
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.0.0-alpha-beta+build.7"),
            RepresentativeServerFixture.Create().Tools);
        var options = CreateDefaultOptions();

        var result = await emitter.EmitAsync(server, options);
        var packageJson = GetFileContents(result, "package.json");
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains("\"version\": \"1.0.0-alpha-beta+build.7\"", packageJson, StringComparison.Ordinal);
        Assert.Contains("version: \"1.0.0-alpha-beta+build.7\"", indexTs, StringComparison.Ordinal);
        Assert.DoesNotContain("\"version\": \"0.0.0\"", packageJson, StringComparison.Ordinal);
        Assert.DoesNotContain("version: \"0.0.0\"", indexTs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_InvalidModuleName_FallsBackToDefaultPackageName()
    {
        var emitter = new TypeScriptEmitter();
        var server = new McpServerDefinition(
            new ModuleDefinition("!!!", "1.2.3"),
            RepresentativeServerFixture.Create().Tools);
        var options = CreateDefaultOptions();

        var result = await emitter.EmitAsync(server, options);
        var packageJson = GetFileContents(result, "package.json");

        Assert.Contains("\"name\": \"ps2mcp-generated-mcp-server\"", packageJson, StringComparison.Ordinal);
    }

    [NpmFact]
    public async Task EmitAsync_RepresentativeFixture_TypeScriptCompilesWithoutDiagnostics()
    {
        var result = await EmitRepresentativeAsync();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "ps2mcp-typescript-emitter-tests", Guid.NewGuid().ToString("N"));

        try
        {
            WriteEmittedFiles(outputDirectory, result.Files);

            var install = await RunProcess(
                outputDirectory,
                GetNpmExecutableName(),
                "install",
                "--ignore-scripts",
                "--no-fund",
                "--no-audit",
                "--package-lock=false");
            Assert.Equal(0, install.ExitCode);

            var check = await RunProcess(outputDirectory, GetNpmExecutableName(), "run", "check");
            Assert.True(
                check.ExitCode == 0,
                $"Generated TypeScript failed to compile.{Environment.NewLine}STDOUT:{Environment.NewLine}{check.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{check.StandardError}");
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EmitAsync_NullServer_ThrowsArgumentNullException()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();

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
        var options = CreateDefaultOptions();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => emitter.EmitAsync(server, options, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task EmitAsync_ToolNamesThatNormalizeToSameIdentifier_UseUniqueSchemaConstNames()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
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
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);
        var toolB = new ToolDefinition(
            ToolName: "get_demo",
            SourceCommand: "Get-DemoAlt",
            Description: "Second tool.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: sharedSchema,
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(toolA, toolB));

        var result = await emitter.EmitAsync(server, options);
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("const getDemoInputSchema = z.object({", contents, StringComparison.Ordinal);
        Assert.Contains("const getDemoInputSchema2 = z.object({", contents, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(contents, Regex.Escape("const getDemoInputSchema"), RegexOptions.CultureInvariant).Count);
    }

    [Fact]
    public async Task EmitAsync_AllCapsToolName_NormalizesSegmentCasing()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
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
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("const getFooInputSchema = z.object({", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_ArraySchemaWithoutItems_FallsBackToUnknownElementType()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
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
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("MysteryValues: z.array(z.unknown()).optional()", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_StringEnumWithPattern_UsesRefineInsteadOfRegexOnEnum()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
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
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = GetFileContents(result, "src/index.ts");

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
        var options = CreateDefaultOptions();
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
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains($"Value: {expectedExpression}", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_IntegerEnum_RendersUnionOfLiterals()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Priority",
                    Type: "integer",
                    Enum: ImmutableArray.Create("1", "2", "3"),
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
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("Priority: z.union([z.number().int().literal(1), z.number().int().literal(2), z.number().int().literal(3)])", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_NumberEnum_RendersUnionOfLiterals()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Score",
                    Type: "number",
                    Enum: ImmutableArray.Create("1.5", "2.5"),
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
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("Score: z.union([z.number().literal(1.5), z.number().literal(2.5)])", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_StringWithPattern_RendersRegex()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
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
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("Email: z.string().regex(new RegExp(\"^.+@.+\\\\..+$\"))", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_InvalidRegexPattern_Throws()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Bad",
                    Type: "string",
                    Enum: null,
                    Minimum: null,
                    Maximum: null,
                    Pattern: "(unclosed",
                    Schema: null)),
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var tool = CreateTool(schema);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => emitter.EmitAsync(server, options));
        Assert.Contains("(unclosed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_NumericConstraints_RendersMinMax()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
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
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("Count: z.number().int().min(1).max(100)", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_RequiredProperty_OmitsOptional()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
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
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("Required: z.string(),", contents, StringComparison.Ordinal);
        Assert.Contains("Optional: z.string().optional(),", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_NestedObjectProperty_RendersMultiLine()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
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
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("Config: z.object({", contents, StringComparison.Ordinal);
        Assert.Contains("Id: z.number().int()", contents, StringComparison.Ordinal);
        Assert.Contains("})", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_EmptyObjectSchema_RendersEmptyObjectBraces()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
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
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("z.object({", contents, StringComparison.Ordinal);
        Assert.Contains("});", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_UnknownType_RendersZodUnknown()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
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
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("Mystery: z.unknown().optional()", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_EmptyToolDescription_EmitsEmptyString()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
        var tool = new ToolDefinition(
            ToolName: "empty_desc_tool",
            SourceCommand: "Get-EmptyDesc",
            Description: "",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: new SchemaDefinition("object", [], [], null),
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("description: \"\",", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_NullToolDescription_EmitsEmptyString()
    {
        var emitter = new TypeScriptEmitter();
        var options = CreateDefaultOptions();
        var tool = new ToolDefinition(
            ToolName: "null_desc_tool",
            SourceCommand: "Get-NullDesc",
            Description: null!,
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: new SchemaDefinition("object", [], [], null),
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.2.3"),
            ImmutableArray.Create(tool));

        var result = await emitter.EmitAsync(server, options);
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("description: \"\",", contents, StringComparison.Ordinal);
    }

    private static EmitOptions CreateDefaultOptions() =>
        new("./modules/Demo.Module/Demo.Module.psd1");

    private static async Task<EmitResult> EmitRepresentativeAsync() =>
        await new TypeScriptEmitter().EmitAsync(RepresentativeServerFixture.Create(), CreateDefaultOptions());

    private static ToolDefinition CreateTool(SchemaDefinition schema) =>
        new(
            ToolName: "test_tool",
            SourceCommand: "Get-Test",
            Description: "Test tool.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: schema,
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);

    private static void WriteEmittedFiles(string outputDirectory, ImmutableArray<EmittedFile> files)
    {
        Directory.CreateDirectory(outputDirectory);

        foreach (var file in files)
        {
            var path = Path.Combine(outputDirectory, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Contents);
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunProcess(string workingDirectory, string fileName, params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string GetNpmExecutableName() => NpmFactAttribute.NpmExecutableName;

    private static string GetFileContents(EmitResult result, string relativePath) =>
        Assert.Single(result.Files.Where(file => string.Equals(file.RelativePath, relativePath, StringComparison.Ordinal))).Contents;

    private static string ReadEmbeddedText(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

}

internal sealed class NpmFactAttribute : FactAttribute
{
    internal static string NpmExecutableName => OperatingSystem.IsWindows() ? "npm.cmd" : "npm";

    public NpmFactAttribute()
    {
        if (!IsNpmAvailable())
        {
            Skip = "npm is required for this generated TypeScript compile smoke test.";
        }
    }

    private static bool IsNpmAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = NpmExecutableName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("--version");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

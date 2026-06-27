using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    public async Task EmitAsync_RepresentativeFixture_ProducesExactlyThreeFiles()
    {
        var result = await EmitRepresentativeAsync();
        var files = result.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);

        Assert.Equal(3, files.Count);
        Assert.Empty(result.Files.Where(file => file.RelativePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_IndexTsMatchesSnapshot()
    {
        var result = await EmitRepresentativeAsync();
        var files = result.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);

        Assert.Equal(ReadEmbeddedText("Ps2Mcp.Emitters.TypeScript.Tests.Snapshots.RepresentativeIndex.ts"), files["src/index.ts"].Contents);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_PackageJsonMatchesSnapshot()
    {
        var result = await EmitRepresentativeAsync();
        var files = result.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);

        Assert.Equal(ReadEmbeddedText("Ps2Mcp.Emitters.TypeScript.Tests.Snapshots.RepresentativePackage.json"), files["package.json"].Contents);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_TsconfigJsonMatchesSnapshot()
    {
        var result = await EmitRepresentativeAsync();
        var files = result.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);

        Assert.Equal(ReadEmbeddedText("Ps2Mcp.Emitters.TypeScript.Tests.Snapshots.RepresentativeTsconfig.json"), files["tsconfig.json"].Contents);
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
        var section = ExtractSection(indexTs, "  const child = spawn(", "  );");

        await AssertSnapshotAsync("SpawnFlags.ts", section);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_DriverScriptContainsRequiredStatements()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var section = ExtractSection(indexTs, "const invokePowerShellCommandScript = [", "].join(\"; \");");

        await AssertSnapshotAsync("DriverScript.ts", section);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_DriverScriptJoinsWithSemicolons()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var section = ExtractSection(indexTs, "const invokePowerShellCommandScript = [", "].join(\"; \");");

        await AssertSnapshotAsync("DriverScript.ts", section);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_TransmitsArgumentsViaStandardInput()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var spawnSection = ExtractSection(indexTs, "  const child = spawn(", "  );");

        await AssertSnapshotAsync("SpawnFlags.ts", spawnSection);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_ParsesOptionalProfileArgument()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var section = ExtractSection(indexTs, "type RuntimeOptions = {", "const runtimeOptions = parseRuntimeOptions(process.argv.slice(2));");

        await AssertSnapshotAsync("ProfileHandling.ts", section);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_LeavesProfileOptionalAtRuntime()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var section = ExtractSection(indexTs, "type RuntimeOptions = {", "const runtimeOptions = parseRuntimeOptions(process.argv.slice(2));");

        await AssertSnapshotAsync("ProfileHandling.ts", section);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_FailsClearlyWhenProfileFileIsMissing()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var section = ExtractSection(indexTs, "const invokePowerShellCommandScript = [", "].join(\"; \");");

        await AssertSnapshotAsync("DriverScript.ts", section);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_UsesDefaultSerializationDepth()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var driverSection = ExtractSection(indexTs, "const invokePowerShellCommandScript = [", "].join(\"; \");");
        var spawnSection = ExtractSection(indexTs, "  const child = spawn(", "  );");

        Assert.Contains(
            $"async (args) => invokePowerShellTool(\"Get-DemoItem\", args, {ExecutionDefinition.DefaultSerializationDepth}, {ExecutionDefinition.DefaultTimeoutMs}, runtimeOptions.profilePath)",
            indexTs,
            StringComparison.Ordinal);
        await AssertSnapshotAsync("DriverScript.ts", driverSection);
        await AssertSnapshotAsync("SpawnFlags.ts", spawnSection);
    }

    [Fact]
    public async Task EmitAsync_ToolWithCustomSerializationDepth_UsesOverrideInGeneratedInvocation()
    {
        const int customSerializationDepth = 9;

        var tool = CreateTool(
            new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null),
            execution: new ExecutionDefinition(customSerializationDepth, ExecutionDefinition.DefaultTimeoutMs));
        var server = CreateServer(tool);

        var result = await Emitter.EmitAsync(server, DefaultOptions);
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains(
            $"async (args) => invokePowerShellTool(\"Get-Test\", args, {customSerializationDepth}, {ExecutionDefinition.DefaultTimeoutMs}, runtimeOptions.profilePath)",
            indexTs,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"async (args) => invokePowerShellTool(\"Get-Test\", args, {ExecutionDefinition.DefaultSerializationDepth}, {ExecutionDefinition.DefaultTimeoutMs}, runtimeOptions.profilePath)",
            indexTs,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_WiresBundledModuleImportPath()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var section = ExtractSection(indexTs, "  const child = spawn(", "  );");

        await AssertSnapshotAsync("SpawnFlags.ts", section);
    }

    [Fact]
    public async Task EmitAsync_InvalidModuleVersion_FallsBackToDefaultVersionInPackageAndServerMetadata()
    {
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "not-a-semver"),
            RepresentativeServerFixture.Create().Tools);

        var result = await Emitter.EmitAsync(server, DefaultOptions);
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
        var server = new McpServerDefinition(
            new ModuleDefinition("Demo.Module", "1.0.0-alpha-beta+build.7"),
            RepresentativeServerFixture.Create().Tools);

        var result = await Emitter.EmitAsync(server, DefaultOptions);
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
        var server = new McpServerDefinition(
            new ModuleDefinition("!!!", "1.2.3"),
            RepresentativeServerFixture.Create().Tools);

        var result = await Emitter.EmitAsync(server, DefaultOptions);
        var packageJson = GetFileContents(result, "package.json");

        Assert.Contains("\"name\": \"ps2mcp-generated-mcp-server\"", packageJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_TypeScriptCompilesWithoutDiagnostics()
    {
        var npmAvailable = await IsNpmAvailableAsync();
        if (!npmAvailable)
        {
            return;
        }

        var result = await EmitRepresentativeAsync();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "ps2mcp-typescript-emitter-tests", Guid.NewGuid().ToString("N"));

        try
        {
            WriteEmittedFiles(outputDirectory, result.Files);

            var install = await RunProcess(
                outputDirectory,
                OperatingSystem.IsWindows() ? "npm.cmd" : "npm",
                "install",
                "--ignore-scripts",
                "--no-fund",
                "--no-audit",
                "--package-lock=false");
            Assert.Equal(0, install.ExitCode);

            var check = await RunProcess(outputDirectory, OperatingSystem.IsWindows() ? "npm.cmd" : "npm", "run", "check");
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
    public async Task EmitAsync_SchemaCoverage_TypeScriptCompilesWithoutDiagnostics()
    {
        var npmAvailable = await IsNpmAvailableAsync();
        if (!npmAvailable)
        {
            return;
        }

        var result = await Emitter.EmitAsync(SchemaCoverageServerFixture.Create(), DefaultOptions);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "ps2mcp-typescript-emitter-tests", Guid.NewGuid().ToString("N"));

        try
        {
            WriteEmittedFiles(outputDirectory, result.Files);

            var install = await RunProcess(
                outputDirectory,
                OperatingSystem.IsWindows() ? "npm.cmd" : "npm",
                "install",
                "--ignore-scripts",
                "--no-fund",
                "--no-audit",
                "--package-lock=false");
            Assert.Equal(0, install.ExitCode);

            var check = await RunProcess(outputDirectory, OperatingSystem.IsWindows() ? "npm.cmd" : "npm", "run", "check");
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
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => Emitter.EmitAsync(null!, DefaultOptions));

        Assert.Equal("server", ex.ParamName);
    }

    [Fact]
    public async Task EmitAsync_NullOptions_ThrowsArgumentNullException()
    {
        var server = RepresentativeServerFixture.Create();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => Emitter.EmitAsync(server, null!));

        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public async Task EmitAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var server = RepresentativeServerFixture.Create();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Emitter.EmitAsync(server, DefaultOptions, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task EmitAsync_ToolNamesThatNormalizeToSameIdentifier_UseUniqueSchemaConstNames()
    {
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
        var server = CreateServer(toolA, toolB);

        var result = await Emitter.EmitAsync(server, DefaultOptions);
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("const getDemoInputSchema = z.object({", contents, StringComparison.Ordinal);
        Assert.Contains("const getDemoInputSchema2 = z.object({", contents, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(contents, Regex.Escape("const getDemoInputSchema"), RegexOptions.CultureInvariant).Count);
    }

    [Fact]
    public async Task EmitAsync_AllCapsToolName_NormalizesSegmentCasing()
    {
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray<SchemaProperty>.Empty,
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var tool = CreateTool("GET-FOO", schema, "Gets foo.");
        var server = CreateServer(tool);

        var result = await Emitter.EmitAsync(server, DefaultOptions);
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("const getFooInputSchema = z.object({", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_ArraySchemaWithoutItems_FallsBackToUnknownElementType()
    {
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
        var declaration = await GenerateSchemaDeclarationAsync(schema);
        await AssertSnapshotAsync("Schemas/ArrayFallback.ts", declaration);
    }

    [Fact]
    public async Task EmitAsync_StringEnumWithPattern_UsesRefineInsteadOfRegexOnEnum()
    {
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
        var declaration = await GenerateSchemaDeclarationAsync(schema);
        await AssertSnapshotAsync("Schemas/StringEnumWithPattern.ts", declaration);
    }

    [Theory]
    [InlineData("integer", "z.number().int()")]
    [InlineData("number", "z.number()")]
    [InlineData("boolean", "z.boolean()")]
    public async Task EmitAsync_NumericAndBooleanTypes_RenderCorrectZodType(string type, string expectedExpression)
    {
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
        var server = CreateServer(tool);

        var result = await Emitter.EmitAsync(server, DefaultOptions);
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains($"Value: {expectedExpression}", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_IntegerEnum_RendersUnionOfLiterals()
    {
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

        var declaration = await GenerateSchemaDeclarationAsync(schema);

        await AssertSnapshotAsync("Schemas/IntegerEnum.ts", declaration);
    }

    [Fact]
    public async Task EmitAsync_NumberEnum_RendersUnionOfLiterals()
    {
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
        var declaration = await GenerateSchemaDeclarationAsync(schema);
        await AssertSnapshotAsync("Schemas/NumberEnum.ts", declaration);
    }

    [Fact]
    public async Task EmitAsync_IntegerEnumWithMinMax_OmitsMinMax()
    {
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Priority",
                    Type: "integer",
                    Enum: ImmutableArray.Create("1", "2", "3"),
                    Minimum: "1",
                    Maximum: "3",
                    Pattern: null,
                    Schema: null)),
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var declaration = await GenerateSchemaDeclarationAsync(schema);
        await AssertSnapshotAsync("Schemas/IntegerEnumWithMinMax.ts", declaration);
    }

    [Fact]
    public async Task EmitAsync_NumberEnumWithMinMax_OmitsMinMax()
    {
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Score",
                    Type: "number",
                    Enum: ImmutableArray.Create("1.5", "2.5"),
                    Minimum: "1.0",
                    Maximum: "3.0",
                    Pattern: null,
                    Schema: null)),
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var declaration = await GenerateSchemaDeclarationAsync(schema);
        await AssertSnapshotAsync("Schemas/NumberEnumWithMinMax.ts", declaration);
    }

    [Fact]
    public async Task EmitAsync_StringWithPattern_RendersRegex()
    {
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
        var declaration = await GenerateSchemaDeclarationAsync(schema);
        await AssertSnapshotAsync("Schemas/StringWithPattern.ts", declaration);
    }

    [Fact]
    public async Task EmitAsync_InvalidRegexPattern_Throws()
    {
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
        var server = CreateServer(tool);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Emitter.EmitAsync(server, DefaultOptions));
        Assert.Contains("(unclosed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_VulnerableRegexPattern_Throws()
    {
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Input",
                    Type: "string",
                    Enum: null,
                    Minimum: null,
                    Maximum: null,
                    Pattern: "(a+)+$",
                    Schema: null)),
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var tool = CreateTool(schema);
        var server = CreateServer(tool);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Emitter.EmitAsync(server, DefaultOptions));
        Assert.Contains("backtracking", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmitAsync_NumericConstraints_RendersMinMax()
    {
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
        var declaration = await GenerateSchemaDeclarationAsync(schema);
        await AssertSnapshotAsync("Schemas/NumericConstraints.ts", declaration);
    }

    [Fact]
    public async Task EmitAsync_RequiredProperty_OmitsOptional()
    {
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
        var declaration = await GenerateSchemaDeclarationAsync(schema);
        await AssertSnapshotAsync("Schemas/RequiredOptional.ts", declaration);
    }

    [Fact]
    public async Task EmitAsync_NestedObjectProperty_RendersMultiLine()
    {
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
        var declaration = await GenerateSchemaDeclarationAsync(schema);
        await AssertSnapshotAsync("Schemas/NestedObject.ts", declaration);
    }

    [Fact]
    public async Task EmitAsync_EmptyObjectSchema_RendersEmptyObjectBraces()
    {
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray<SchemaProperty>.Empty,
            Required: ImmutableArray<string>.Empty,
            Items: null);
        var declaration = await GenerateSchemaDeclarationAsync(schema);
        await AssertSnapshotAsync("Schemas/EmptyObject.ts", declaration);
    }

    [Fact]
    public async Task EmitAsync_UnknownType_RendersZodUnknown()
    {
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
        var declaration = await GenerateSchemaDeclarationAsync(schema);
        await AssertSnapshotAsync("Schemas/UnknownType.ts", declaration);
    }

    [Fact]
    public async Task EmitAsync_EmptyToolDescription_EmitsEmptyString()
    {
        var tool = new ToolDefinition(
            ToolName: "empty_desc_tool",
            SourceCommand: "Get-EmptyDesc",
            Description: "",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null),
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);
        var server = CreateServer(tool);

        var result = await Emitter.EmitAsync(server, DefaultOptions);
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("description: \"\",", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_NullToolDescription_EmitsEmptyString()
    {
        var tool = new ToolDefinition(
            ToolName: "null_desc_tool",
            SourceCommand: "Get-NullDesc",
            Description: null,
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null),
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);
        var server = CreateServer(tool);

        var result = await Emitter.EmitAsync(server, DefaultOptions);
        var contents = GetFileContents(result, "src/index.ts");

        Assert.Contains("description: \"\",", contents, StringComparison.Ordinal);
    }

    private static readonly TypeScriptEmitter Emitter = new();

    private static EmitOptions DefaultOptions => new("./modules/Demo.Module/Demo.Module.psd1");

    private static async Task<EmitResult> EmitRepresentativeAsync() =>
        await Emitter.EmitAsync(RepresentativeServerFixture.Create(), DefaultOptions);

    private static ToolDefinition CreateTool(SchemaDefinition schema, ExecutionDefinition? execution = null) =>
        new(
            ToolName: "test_tool",
            SourceCommand: "Get-Test",
            Description: "Test tool.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: schema,
            Execution: execution ?? new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);

    private static ToolDefinition CreateTool(
        string name,
        SchemaDefinition schema,
        string? description = null,
        ExecutionDefinition? execution = null) =>
        new(
            ToolName: name,
            SourceCommand: $"Get-{name}",
            Description: description ?? $"Tests {name}.",
            Parameters: ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: schema,
            Execution: execution ?? new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);

    private static McpServerDefinition CreateServer(params ToolDefinition[] tools) =>
        new(new ModuleDefinition("Demo.Module", "1.2.3"), ImmutableArray.Create(tools));

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

        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static async Task<bool> IsNpmAvailableAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "npm.cmd" : "npm",
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

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private static string GetFileContents(EmitResult result, string relativePath) =>
        Assert.Single(result.Files.Where(file => string.Equals(file.RelativePath, relativePath, StringComparison.Ordinal))).Contents;

    private static string ReadEmbeddedText(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static async Task AssertSnapshotAsync(string snapshotRelativePath, string actual)
    {
        var resourceName = $"Ps2Mcp.Emitters.TypeScript.Tests.Snapshots.{snapshotRelativePath.Replace('/', '.')}";
        var expected = ReadEmbeddedText(resourceName);
        Assert.Equal(expected, actual);
    }

    private static string ExtractSection(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker '{startMarker}' not found in source.");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"End marker '{endMarker}' not found in source after position {start}.");
        return source.Substring(start, end - start + endMarker.Length);
    }

    private static async Task<string> GenerateSchemaDeclarationAsync(SchemaDefinition schema, string? toolName = null)
    {
        var tool = CreateTool(schema);
        var server = CreateServer(tool);
        var result = await Emitter.EmitAsync(server, DefaultOptions);
        var contents = GetFileContents(result, "src/index.ts");
        var schemaIdentifier = $"const {GetSchemaIdentifierBase(toolName ?? tool.ToolName)}InputSchema";
        return ExtractSection(contents, $"{schemaIdentifier} = ", ";\n");
    }

    private static string GetSchemaIdentifierBase(string toolName)
    {
        var segments = toolName.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return "tool";
        }

        var builder = new StringBuilder();
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            var normalized = char.ToUpperInvariant(segment[0]) + segment[1..].ToLower(System.Globalization.CultureInfo.InvariantCulture);
            if (index == 0)
            {
                normalized = char.ToLowerInvariant(normalized[0]) + normalized[1..];
            }

            builder.Append(normalized);
        }

        if (!char.IsLetter(builder[0]) && builder[0] != '_')
        {
            builder.Insert(0, "tool");
        }

        return builder.ToString();
    }

}

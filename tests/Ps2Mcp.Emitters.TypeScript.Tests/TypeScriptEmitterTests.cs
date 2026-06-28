using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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
    public async Task EmitAsync_RepresentativeFixture_SpawnCallBlock_MatchesSnapshot()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var section = ExtractSection(indexTs, "  const child = spawn(", "  );");

        await AssertSnapshotAsync("SpawnFlags.ts", section);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_DriverScriptSection_MatchesSnapshot()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var section = ExtractSection(indexTs, "const invokePowerShellCommandScript = `", "`;");

        await AssertSnapshotAsync("DriverScript.ts", section);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_DriverScriptIsTemplateLiteral()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains("const invokePowerShellCommandScript = `", indexTs, StringComparison.Ordinal);
        Assert.DoesNotContain("const invokePowerShellCommandScript = [", indexTs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_DriverScriptParsesInPowerShell()
    {
        var pwshAvailable = await IsPwshAvailableAsync();
        if (!pwshAvailable)
        {
            return;
        }

        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var script = ExtractPowerShellScript(indexTs);

        var tempFile = Path.Combine(Path.GetTempPath(), "ps2mcp-test", $"{Guid.NewGuid():N}.ps1");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tempFile)!);
            File.WriteAllText(tempFile, script);

            var (exitCode, _, stderr) = await RunProcess(
                Path.GetTempPath(),
                OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh",
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                $". {{ [System.Management.Automation.Language.Parser]::ParseFile('{tempFile.Replace("'", "''")}', [ref]$null, [ref]$null) | Out-Null }}");

            Assert.True(
                exitCode == 0,
                $"PowerShell script failed to parse.{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_ProfileHandlingBlock_MatchesSnapshot()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");
        var section = ExtractSection(indexTs, "type RuntimeOptions = {", "const runtimeOptions = parseRuntimeOptions(process.argv.slice(2));");

        await AssertSnapshotAsync("ProfileHandling.ts", section);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_UsesDefaultSerializationDepth()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains(
            $"async (args) => invokePowerShellTool(\"Get-DemoItem\", args, [\"Secret\"], {ExecutionDefinition.DefaultSerializationDepth}, {ExecutionDefinition.DefaultTimeoutMs}, runtimeOptions.profilePath)",
            indexTs,
            StringComparison.Ordinal);
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
            $"async (args) => invokePowerShellTool(\"Get-Test\", args, [], {customSerializationDepth}, {ExecutionDefinition.DefaultTimeoutMs}, runtimeOptions.profilePath)",
            indexTs,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"async (args) => invokePowerShellTool(\"Get-Test\", args, [], {ExecutionDefinition.DefaultSerializationDepth}, {ExecutionDefinition.DefaultTimeoutMs}, runtimeOptions.profilePath)",
            indexTs,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_SecureStringParameter_ConvertsValueInsidePowerShellAndMarksSecretDescription()
    {
        var schema = new SchemaDefinition(
            Type: "object",
            Properties: ImmutableArray.Create(
                new SchemaProperty(
                    Name: "Secret",
                    Type: "string",
                    Enum: null,
                    Minimum: null,
                    Maximum: null,
                    Pattern: null,
                    Schema: null)),
            Required: ImmutableArray.Create("Secret"),
            Items: null);
        var secureParameter = new ParameterDefinition(
            Name: "Secret",
            Type: "SecureString",
            IsMandatory: true,
            IsSecure: true,
            Description: "A secret token.",
            DefaultValue: null,
            Aliases: ImmutableArray<string>.Empty,
            ParameterSets: ImmutableArray.Create("Default"));
        var tool = CreateTool(schema, parameters: ImmutableArray.Create(secureParameter));
        var server = CreateServer(tool);

        var result = await Emitter.EmitAsync(server, DefaultOptions);
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains(
            "Secret: z.string().describe(\"A secret token. Treated as a secret.\")",
            indexTs,
            StringComparison.Ordinal);
        Assert.Contains("PS2MCP_SECURE_PARAMETER_NAMES: JSON.stringify(secureParameterNames)", indexTs, StringComparison.Ordinal);
        Assert.Contains("ConvertTo-SecureString -String $secureValue -AsPlainText -Force", indexTs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitAsync_RepresentativeFixture_MapsPowerShellFailuresToStructuredErrorPayloads()
    {
        var result = await EmitRepresentativeAsync();
        var indexTs = GetFileContents(result, "src/index.ts");

        Assert.Contains("'invalid input'", indexTs, StringComparison.Ordinal);
        Assert.Contains("'module load failure'", indexTs, StringComparison.Ordinal);
        Assert.Contains("'bootstrap profile failure'", indexTs, StringComparison.Ordinal);
        Assert.Contains("'command execution failure'", indexTs, StringComparison.Ordinal);
        Assert.Contains("'serialization failure'", indexTs, StringComparison.Ordinal);
        Assert.Contains("'runtime internal error'", indexTs, StringComparison.Ordinal);
        Assert.Contains("function parsePowerShellError(", indexTs, StringComparison.Ordinal);
        Assert.Contains("isError: true", indexTs, StringComparison.Ordinal);
        Assert.Contains("JSON.stringify(errorPayload)", indexTs, StringComparison.Ordinal);
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
    public async Task EmitAsync_RepresentativeFixture_ToolCallRoundTripsSuccessfully()
    {
        var npmAvailable = await IsNpmAvailableAsync();
        var pwshAvailable = await IsPwshAvailableAsync();
        if (!npmAvailable || !pwshAvailable)
        {
            return;
        }

        var (moduleDir, moduleName) = CreateTempPowerShellModule();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "ps2mcp-roundtrip", Guid.NewGuid().ToString("N"));
        Process? serverProcess = null;

        try
        {
            var result = await Emitter.EmitAsync(RepresentativeServerFixture.Create(), DefaultOptions);
            WriteEmittedFiles(outputDirectory, result.Files);

            var bundledModuleDir = Path.Combine(outputDirectory, "src", "modules", moduleName);
            Directory.CreateDirectory(bundledModuleDir);
            foreach (var sourceFile in Directory.GetFiles(moduleDir))
            {
                File.Copy(sourceFile, Path.Combine(bundledModuleDir, Path.GetFileName(sourceFile)), overwrite: true);
            }

            var install = await RunProcess(
                outputDirectory,
                OperatingSystem.IsWindows() ? "npm.cmd" : "npm",
                "install",
                "--ignore-scripts",
                "--no-fund",
                "--no-audit",
                "--package-lock=false");
            Assert.Equal(0, install.ExitCode);

            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "npx.cmd" : "npx",
                WorkingDirectory = outputDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("tsx");
            startInfo.ArgumentList.Add("src/index.ts");

            serverProcess = Process.Start(startInfo);
            Assert.NotNull(serverProcess);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await SendMcpMessageAsync(serverProcess.StandardInput, new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { },
                    clientInfo = new { name = "test", version = "1.0.0" },
                },
            }, cts.Token);

            var initResponse = await ReadMcpMessageAsync(serverProcess.StandardOutput, cts.Token);
            Assert.NotNull(initResponse);
            Assert.Equal(1, initResponse.Value.GetProperty("id").GetInt32());

            await SendMcpMessageAsync(serverProcess.StandardInput, new
            {
                jsonrpc = "2.0",
                method = "notifications/initialized",
            }, cts.Token);

            await SendMcpMessageAsync(serverProcess.StandardInput, new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/list",
            }, cts.Token);

            var listResponse = await ReadMcpMessageAsync(serverProcess.StandardOutput, cts.Token);
            Assert.NotNull(listResponse);
            Assert.Equal(2, listResponse.Value.GetProperty("id").GetInt32());

            await SendMcpMessageAsync(serverProcess.StandardInput, new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "tools/call",
                @params = new
                {
                    name = "get_demo_item",
                    arguments = new { Name = "hello" },
                },
            }, cts.Token);

            var callResponse = await ReadMcpMessageAsync(serverProcess.StandardOutput, cts.Token);
            Assert.NotNull(callResponse);
            Assert.Equal(3, callResponse.Value.GetProperty("id").GetInt32());

            var responseJson = callResponse.Value.GetRawText();

            Assert.False(
                callResponse.Value.TryGetProperty("error", out _),
                $"Server returned error: {responseJson}");

            var resultElement = callResponse.Value.GetProperty("result");

            var isErrorPresent = resultElement.TryGetProperty("isError", out var isErrorElement);
            Assert.False(isErrorPresent && isErrorElement.GetBoolean(), $"Server returned error: {responseJson}");

            var contentText = resultElement.GetProperty("content")[0].GetProperty("text").GetString()!;
            Assert.Contains("\"Name\":\"hello\"", contentText, StringComparison.Ordinal);
        }
        finally
        {
            if (serverProcess is { HasExited: false })
            {
                serverProcess.Kill(entireProcessTree: true);
            }

            serverProcess?.Dispose();

            await Task.Delay(500, CancellationToken.None);

            TryDeleteDirectory(outputDirectory);
            TryDeleteDirectory(moduleDir);
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

    private static ToolDefinition CreateTool(
        SchemaDefinition schema,
        ExecutionDefinition? execution = null,
        ImmutableArray<ParameterDefinition>? parameters = null) =>
        new(
            ToolName: "test_tool",
            SourceCommand: "Get-Test",
            Description: "Test tool.",
            Parameters: parameters ?? ImmutableArray<ParameterDefinition>.Empty,
            RequiredParameterSet: null,
            Schema: schema,
            Execution: execution ?? new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth, ExecutionDefinition.DefaultTimeoutMs),
            Help: null,
            Output: null);

    private static ToolDefinition CreateTool(
        string name,
        SchemaDefinition schema,
        string? description = null,
        ExecutionDefinition? execution = null,
        ImmutableArray<ParameterDefinition>? parameters = null) =>
        new(
            ToolName: name,
            SourceCommand: $"Get-{name}",
            Description: description ?? $"Tests {name}.",
            Parameters: parameters ?? ImmutableArray<ParameterDefinition>.Empty,
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

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
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

    private static async Task<bool> IsPwshAvailableAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh",
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

    private static string ExtractPowerShellScript(string indexTs)
    {
        var startMarker = "const invokePowerShellCommandScript = `";
        var endMarker = "`;";
        var start = indexTs.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker '{startMarker}' not found in source.");
        var scriptStart = start + startMarker.Length;
        var end = indexTs.IndexOf(endMarker, scriptStart, StringComparison.Ordinal);
        Assert.True(end >= 0, $"End marker '{endMarker}' not found in source after position {scriptStart}.");
        return indexTs.Substring(scriptStart, end - scriptStart);
    }

    private static string GetFileContents(EmitResult result, string relativePath) =>
        Assert.Single(result.Files.Where(file => string.Equals(file.RelativePath, relativePath, StringComparison.Ordinal))).Contents;

    private static string ReadEmbeddedText(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        return StripSnapshotHeader(text);
    }

    private static async Task AssertSnapshotAsync(string snapshotRelativePath, string actual)
    {
        var resourceName = $"Ps2Mcp.Emitters.TypeScript.Tests.Snapshots.{snapshotRelativePath.Replace('/', '.')}";
        var expected = ReadEmbeddedText(resourceName).TrimEnd('\n');
        Assert.Equal(expected, actual.TrimEnd('\n'));
    }

    private static string StripSnapshotHeader(string text)
    {
        const string snapshotHeader = "// Snapshot artifact: auto-generated test fixture. Not runtime source.";
        return text.StartsWith(snapshotHeader + "\n", StringComparison.Ordinal)
            ? text[(snapshotHeader.Length + 1)..]
            : text;
    }

    private static string ExtractSection(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker '{startMarker}' not found in source.");
        var normalizedStart = source.LastIndexOf('\n', start);
        start = normalizedStart < 0 ? start : normalizedStart + 1;
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"End marker '{endMarker}' not found in source after position {start}.");
        var section = source.Substring(start, end - start + endMarker.Length);
        var lines = section.Split('\n');
        var minIndent = lines
            .Where(static line => line.Length > 0)
            .Select(static line => line.TakeWhile(static ch => ch is ' ' or '\t').Count())
            .DefaultIfEmpty(0)
            .Min();

        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Length >= minIndent)
            {
                lines[index] = lines[index][minIndent..];
            }
        }

        return string.Join("\n", lines);
    }

    private static async Task<string> GenerateSchemaDeclarationAsync(SchemaDefinition schema, string? toolName = null)
    {
        var tool = CreateTool(schema);
        var server = CreateServer(tool);
        var result = await Emitter.EmitAsync(server, DefaultOptions);
        var contents = GetFileContents(result, "src/index.ts");
        var schemaIdentifier = $"const {TypeScriptEmitter.GetSchemaIdentifierBase(toolName ?? tool.ToolName)}";
        return ExtractSection(contents, $"{schemaIdentifier} = ", ";\n");
    }

    private static (string ModuleDirectory, string ModuleName) CreateTempPowerShellModule()
    {
        var moduleDir = Path.Combine(Path.GetTempPath(), "ps2mcp-test", $"module-{Guid.NewGuid():N}");
        Directory.CreateDirectory(moduleDir);

        var manifest = @"@{
    RootModule = 'Demo.Module.psm1'
    ModuleVersion = '1.0.0'
    FunctionsToExport = @('Get-DemoItem')
}
";
        File.WriteAllText(Path.Combine(moduleDir, "Demo.Module.psd1"), manifest);

        var module = @"function Get-DemoItem {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Name
    )
    return @{ Name = $Name; Timestamp = (Get-Date).ToString('o') }
}
";
        File.WriteAllText(Path.Combine(moduleDir, "Demo.Module.psm1"), module);

        return (moduleDir, "Demo.Module");
    }

    private static async Task SendMcpMessageAsync(StreamWriter writer, object message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        await writer.WriteAsync(json.AsMemory(), cancellationToken);
        await writer.WriteAsync('\n');
        await writer.FlushAsync(cancellationToken);
    }

    private static async Task<JsonElement?> ReadMcpMessageAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        return JsonSerializer.Deserialize<JsonElement>(line);
    }

}

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ps2Mcp.Core;

namespace Ps2Mcp.Emitters.TypeScript;

/// <summary>
/// Emits a single-file TypeScript MCP server from an <see cref="McpServerDefinition"/>.
/// The output is a self-contained <c>src/index.ts</c> using <c>@modelcontextprotocol/sdk</c> and <c>zod</c> for schema validation.
/// </summary>
public sealed class TypeScriptEmitter : IServerEmitter
{
    private const string IndexFilePath = "src/index.ts";
    private const string PackageJsonFilePath = "package.json";
    private const string TsConfigFilePath = "tsconfig.json";
    private const string DefaultPackageVersion = "0.0.0";
    private const string DefaultPackageName = "ps2mcp-generated-mcp-server";
    private const string NodeEngineRange = ">=22.0.0";
    private const string McpSdkVersion = "^1.0.0";
    private const string ZodVersion = "^3.25.0";
    private const string TypeScriptVersion = "^5.0.0";
    private const string NodeTypesVersion = "^22.0.0";
    private const string TsxVersion = "^4.0.0";
    private const int IndentSize = 2;

    private const string PowerShellScript = """
        $ErrorActionPreference = 'Stop'
        $modulePath = $env:PS2MCP_MODULE_PATH
        $profilePath = $env:PS2MCP_PROFILE_PATH
        $secureParameterNamesJson = $env:PS2MCP_SECURE_PARAMETER_NAMES
        $sourceCommand = $env:PS2MCP_SOURCE_COMMAND
        $serializationDepth = [int]$env:PS2MCP_SERIALIZATION_DEPTH
        function Write-StructuredError {
            param(
                [string]$category,
                [string]$message,
                [string]$details
            )

            $payload = [ordered]@{
                category = $category
                message = $message
                sourceCommand = $sourceCommand
            }

            if (-not [string]::IsNullOrWhiteSpace($details)) {
                $payload.details = $details
            }

            [Console]::Error.WriteLine(($payload | ConvertTo-Json -Compress))
            exit 1
        }

        function Convert-SecureParameterValue {
            param(
                [string]$parameterName,
                $secureValue
            )

            if ($null -eq $secureValue) {
                return $null
            }

            if ($secureValue -is [string]) {
                return ConvertTo-SecureString -String $secureValue -AsPlainText -Force
            }

            if ($secureValue -is [System.Collections.IEnumerable] -and $secureValue -isnot [string]) {
                return @($secureValue | ForEach-Object {
                    if ($_ -isnot [string]) {
                        throw "Secure parameter '$parameterName' must be a string or array of strings."
                    }

                    ConvertTo-SecureString -String $_ -AsPlainText -Force
                })
            }

            throw "Secure parameter '$parameterName' must be a string or array of strings."
        }

        try {
            try {
                $argumentsJson = [Console]::In.ReadToEnd()
                $arguments = if ([string]::IsNullOrWhiteSpace($argumentsJson)) { @{} } else { ConvertFrom-Json -InputObject $argumentsJson -AsHashtable }
                if ($arguments -isnot [System.Collections.IDictionary]) {
                    throw "Tool arguments must deserialize to an object."
                }

                $secureParameterNames = if ([string]::IsNullOrWhiteSpace($secureParameterNamesJson)) { @() } else { @(ConvertFrom-Json -InputObject $secureParameterNamesJson) }
            }
            catch {
                Write-StructuredError 'invalid input' 'Failed to parse tool arguments.' $_.Exception.Message
            }

            if (-not [string]::IsNullOrWhiteSpace($profilePath)) {
                if (-not (Test-Path -LiteralPath $profilePath -PathType Leaf)) {
                    Write-StructuredError 'bootstrap profile failure' "Bootstrap profile file not found: $profilePath" $null
                }

                try {
                    . $profilePath
                }
                catch {
                    Write-StructuredError 'bootstrap profile failure' 'Bootstrap profile failed.' $_.Exception.Message
                }
            }

            try {
                Import-Module -Force $modulePath
            }
            catch {
                Write-StructuredError 'module load failure' 'Failed to import bundled module.' $_.Exception.Message
            }

            try {
                foreach ($secureParameterName in $secureParameterNames) {
                    if ($arguments.Contains($secureParameterName)) {
                        $arguments[$secureParameterName] = Convert-SecureParameterValue -parameterName $secureParameterName -secureValue $arguments[$secureParameterName]
                    }
                }
            }
            catch {
                Write-StructuredError 'invalid input' 'Failed to bind secure parameter values.' $_.Exception.Message
            }

            try {
                $result = & $sourceCommand @arguments
            }
            catch {
                Write-StructuredError 'command execution failure' 'PowerShell command failed.' $_.Exception.Message
            }

            try {
                $result | ConvertTo-Json -Depth $serializationDepth -Compress
            }
            catch {
                Write-StructuredError 'serialization failure' 'Failed to serialize PowerShell output.' $_.Exception.Message
            }
        }
        catch {
            Write-StructuredError 'runtime internal error' 'Unexpected runtime failure.' $_.Exception.Message
        }
        """;

    /// <inheritdoc />
    public async Task<EmitResult> EmitAsync(
        McpServerDefinition server,
        EmitOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var packageJsonFile = new EmittedFile(PackageJsonFilePath, RenderPackageJson(server));
        var tsConfigFile = new EmittedFile(TsConfigFilePath, RenderTsConfigJson());
        var indexFile = new EmittedFile(
            IndexFilePath,
            await RenderIndexAsync(server, options, cancellationToken).ConfigureAwait(false));
        packageJsonFile.Validate();
        tsConfigFile.Validate();
        indexFile.Validate();

        return new EmitResult(ImmutableArray.Create(packageJsonFile, tsConfigFile, indexFile));
    }

    private static string RenderPackageJson(McpServerDefinition server) => WriteJson(json =>
    {
        var resolvedVersion = ResolveModuleVersion(server.Module.Version);

        json.WriteStartObject();
        json.WriteString("name", GetPackageName(server.Module.Name));
        json.WriteString("version", resolvedVersion);
        json.WriteString("description", $"Generated MCP server for {server.Module.Name}.");
        json.WriteString("type", "module");
        json.WriteString("main", "./src/index.ts");
        json.WriteBoolean("private", true);

        json.WritePropertyName("engines");
        json.WriteStartObject();
        json.WriteString("node", NodeEngineRange);
        json.WriteEndObject();

        json.WritePropertyName("scripts");
        json.WriteStartObject();
        json.WriteString("start", "tsx src/index.ts");
        json.WriteString("check", "tsc --noEmit");
        json.WriteEndObject();

        json.WritePropertyName("dependencies");
        json.WriteStartObject();
        json.WriteString("@modelcontextprotocol/sdk", McpSdkVersion);
        json.WriteString("zod", ZodVersion);
        json.WriteEndObject();

        json.WritePropertyName("devDependencies");
        json.WriteStartObject();
        json.WriteString("@types/node", NodeTypesVersion);
        json.WriteString("tsx", TsxVersion);
        json.WriteString("typescript", TypeScriptVersion);
        json.WriteEndObject();

        json.WriteEndObject();
    });

    private static string RenderTsConfigJson() => WriteJson(json =>
    {
        json.WriteStartObject();

        json.WritePropertyName("compilerOptions");
        json.WriteStartObject();
        json.WriteString("target", "ES2022");
        json.WriteString("module", "NodeNext");
        json.WriteString("moduleResolution", "NodeNext");
        json.WriteBoolean("strict", true);
        json.WriteBoolean("noEmit", true);
        json.WriteBoolean("skipLibCheck", true);
        json.WriteBoolean("verbatimModuleSyntax", true);
        json.WriteBoolean("allowImportingTsExtensions", true);
        json.WritePropertyName("types");
        json.WriteStartArray();
        json.WriteStringValue("node");
        json.WriteEndArray();
        json.WriteEndObject();

        json.WritePropertyName("include");
        json.WriteStartArray();
        json.WriteStringValue("src/**/*.ts");
        json.WriteEndArray();

        json.WriteEndObject();
    });

    private static async Task<string> RenderIndexAsync(
        McpServerDefinition server,
        EmitOptions options,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var schemaIdentifiers = CreateSchemaIdentifiers(server.Tools);
        var resolvedVersion = ResolveModuleVersion(server.Module.Version);

        RenderImports(builder);
        RenderConstants(builder, options);
        RenderRuntimeOptions(builder);
        RenderPowerShellErrorHelpers(builder);
        await RenderSchemaDeclarationsAsync(builder, server.Tools, schemaIdentifiers, cancellationToken).ConfigureAwait(false);
        RenderServerDeclaration(builder, server.Module.Name, resolvedVersion);
        RenderToolRegistrations(builder, server.Tools, schemaIdentifiers, cancellationToken);
        RenderInvokePowerShellTool(builder);
        RenderMain(builder);

        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void RenderImports(StringBuilder builder)
    {
        builder.AppendLine("import { spawn } from \"node:child_process\";");
        builder.AppendLine("import { dirname, resolve } from \"node:path\";");
        builder.AppendLine("import { fileURLToPath } from \"node:url\";");
        builder.AppendLine("import { McpServer } from \"@modelcontextprotocol/sdk/server/mcp.js\";");
        builder.AppendLine("import { StdioServerTransport } from \"@modelcontextprotocol/sdk/server/stdio.js\";");
        builder.AppendLine("import { z } from \"zod\";");
        builder.AppendLine();
    }

    private static void RenderConstants(StringBuilder builder, EmitOptions options)
    {
        builder.AppendLine("const runtimeDirectory = dirname(fileURLToPath(import.meta.url));");
        builder.Append("const bundledModuleImportPath = resolve(runtimeDirectory, ")
            .Append(QuoteTypeScriptString(options.BundledModuleImportPath))
            .AppendLine(");");
        builder.AppendLine("const invokePowerShellCommandScript = `");
        builder.AppendLine(PowerShellScript);
        builder.AppendLine("`;");
        builder.AppendLine();
    }

    private static void RenderRuntimeOptions(StringBuilder builder)
    {
        builder.Append("""
            type RuntimeOptions = {
              profilePath?: string;
            };

            function parseRuntimeOptions(argv: string[]): RuntimeOptions {
              let profilePath: string | undefined;

              for (let index = 0; index < argv.length; index += 1) {
                const argument = argv[index];
                if (argument === "--profile") {
                  if (profilePath !== undefined) {
                    throw new Error("Runtime argument \"--profile\" may be specified at most once.");
                  }

                  index += 1;
                  const value = argv[index];
                  if (value === undefined || value.length === 0) {
                    throw new Error("Runtime argument \"--profile\" requires a path value.");
                  }

                  profilePath = value;
                  continue;
                }

                throw new Error(`Unknown runtime argument: ${argument}`);
              }

              return { profilePath };
            }

            const runtimeOptions = parseRuntimeOptions(process.argv.slice(2));

            """);
        builder.AppendLine();
    }

    private static async Task RenderSchemaDeclarationsAsync(
        StringBuilder builder,
        ImmutableArray<ToolDefinition> tools,
        IReadOnlyDictionary<string, string> schemaIdentifiers,
        CancellationToken cancellationToken)
    {
        foreach (var tool in tools)
        {
            cancellationToken.ThrowIfCancellationRequested();

            builder.Append("const ")
                .Append(schemaIdentifiers[tool.ToolName])
                .Append(" = ")
                .Append(await RenderSchemaDefinitionAsync(
                    tool.Schema,
                    0,
                    CreateParameterLookup(tool.Parameters),
                    cancellationToken).ConfigureAwait(false))
                .AppendLine(";");
            builder.AppendLine();
        }
    }

    private static void RenderServerDeclaration(StringBuilder builder, string moduleName, string resolvedVersion)
    {
        builder.AppendLine("const server = new McpServer({");
        builder.Append("  name: ")
            .Append(QuoteTypeScriptString(moduleName))
            .AppendLine(",");
        builder.Append("  version: ")
            .Append(QuoteTypeScriptString(resolvedVersion))
            .AppendLine(",");
        builder.AppendLine("});");
        builder.AppendLine();
    }

    private static void RenderToolRegistrations(
        StringBuilder builder,
        ImmutableArray<ToolDefinition> tools,
        IReadOnlyDictionary<string, string> schemaIdentifiers,
        CancellationToken cancellationToken)
    {
        foreach (var tool in tools)
        {
            cancellationToken.ThrowIfCancellationRequested();

            builder.AppendLine("server.registerTool(");
            builder.Append("  ")
                .Append(QuoteTypeScriptString(tool.ToolName))
                .AppendLine(",");
            builder.AppendLine("  {");
            builder.Append("    description: ")
                .Append(QuoteTypeScriptString(tool.Description ?? string.Empty))
                .AppendLine(",");
            builder.Append("    inputSchema: ")
                .Append(schemaIdentifiers[tool.ToolName])
                .AppendLine(",");
            builder.AppendLine("  },");
            builder.Append("  async (args) => invokePowerShellTool(")
                .Append(QuoteTypeScriptString(tool.SourceCommand))
                .Append(", args, ")
                .Append(RenderTypeScriptStringArray(GetSecureStringParameterNames(tool.Parameters)))
                .Append(", ")
                .Append(tool.Execution.SerializationDepth.ToString(CultureInfo.InvariantCulture))
                .Append(", ")
                .Append(tool.Execution.TimeoutMs.ToString(CultureInfo.InvariantCulture))
                .Append(", runtimeOptions.profilePath")
                .AppendLine("),");
            builder.AppendLine(");");
            builder.AppendLine();
        }
    }

    private static void RenderInvokePowerShellTool(StringBuilder builder)
    {
        builder.Append(StripMargin("""
                        |async function invokePowerShellTool(
                        |  sourceCommand: string,
                        |  args: unknown,
                        |  secureParameterNames: string[],
                        |  serializationDepth: number,
                        |  timeoutMs: number,
                        |  profilePath: string | undefined,
                        |): Promise<{ content: Array<{ type: "text"; text: string }>; isError?: boolean }> {
                        |  let argsJson: string;
                        |  try {
                        |    argsJson = args === undefined ? "{}" : JSON.stringify(args);
                        |  }
                        |  catch (error) {
                        |    const errorPayload = createRuntimeInternalError(
                        |      sourceCommand,
                        |      `PowerShell invocation for ${sourceCommand} failed before a structured error payload was produced.`,
                        |      error instanceof Error ? error.message : String(error),
                        |    );
                        |    return {
                        |      isError: true,
                        |      content: [{ type: "text", text: JSON.stringify(errorPayload) }],
                        |    };
                        |  }
                        |
                        |  const child = spawn(
                        |    "pwsh",
                        |    [
                        |      "-NoProfile",
                        |      "-NonInteractive",
                        |      "-Command",
                        |      invokePowerShellCommandScript,
                        |    ],
                        |    {
                        |      env: {
                        |        ...process.env,
                        |        PS2MCP_MODULE_PATH: bundledModuleImportPath,
                        |        PS2MCP_PROFILE_PATH: profilePath ?? "",
                        |        PS2MCP_SECURE_PARAMETER_NAMES: JSON.stringify(secureParameterNames),
                        |        PS2MCP_SERIALIZATION_DEPTH: serializationDepth.toString(10),
                        |        PS2MCP_SOURCE_COMMAND: sourceCommand,
                        |      },
                        |      stdio: ["pipe", "pipe", "pipe"],
                        |    },
                        |  );
                        |
                        |  child.stdout.setEncoding("utf8");
                        |  child.stderr.setEncoding("utf8");
                        |
                        |  const stdoutChunks: string[] = [];
                        |  const stderrChunks: string[] = [];
                        |  child.stdout.on("data", (chunk: string) => stdoutChunks.push(chunk));
                        |  child.stderr.on("data", (chunk: string) => stderrChunks.push(chunk));
                        |
                        |  try {
                        |    const exitCode = await new Promise<number | null>((resolveClose, reject) => {
                        |      const onError = (err: Error) => {
                        |        cleanup();
                        |        reject(err);
                        |      };
                        |      const onClose = (code: number | null) => {
                        |        cleanup();
                        |        resolveClose(code);
                        |      };
                        |      const timer = setTimeout(() => {
                        |        cleanup();
                        |        child.kill();
                        |        reject(new Error(
                        |          `PowerShell invocation for ${sourceCommand} exceeded timeout of ${timeoutMs}ms and was terminated.`,
                        |        ));
                        |      }, timeoutMs);
                        |      const cleanup = () => {
                        |        clearTimeout(timer);
                        |        child.stdin.off("error", onError);
                        |        child.off("error", onError);
                        |        child.off("close", onClose);
                        |      };
                        |      child.stdin.on("error", onError);
                        |      child.stdin.end(argsJson, "utf8");
                        |      child.once("error", onError);
                        |      child.once("close", onClose);
                        |    });
                        |
                        |    const stderr = stderrChunks.join("").trim();
                        |    if ((exitCode ?? 1) !== 0) {
                        |      const errorPayload = parsePowerShellError(stderr, sourceCommand);
                        |      return {
                        |        isError: true,
                        |        content: [{ type: "text", text: JSON.stringify(errorPayload) }],
                        |      };
                        |    }
                        |
                        |    const stdout = stdoutChunks.join("").trim();
                        |    const content: Array<{ type: "text"; text: string }> = [
                        |      { type: "text", text: stdout.length === 0 ? "null" : stdout },
                        |    ];
                        |    if (stderr.length > 0) {
                        |      content.push({ type: "text", text: stderr });
                        |    }
                        |
                        |    return { content };
                        |  }
                        |  catch (error) {
                        |    const errorPayload = createRuntimeInternalError(
                        |      sourceCommand,
                        |      `PowerShell invocation for ${sourceCommand} failed before a structured error payload was produced.`,
                        |      error instanceof Error ? error.message : String(error),
                        |    );
                        |    return {
                        |      isError: true,
                        |      content: [{ type: "text", text: JSON.stringify(errorPayload) }],
                        |    };
                        |  }
                        |}
                        |
                        """));
        builder.AppendLine();
    }

    private static void RenderPowerShellErrorHelpers(StringBuilder builder)
    {
        builder.Append(StripMargin("""
                        |type PowerShellErrorCategory =
                        |  | "invalid input"
                        |  | "module load failure"
                        |  | "bootstrap profile failure"
                        |  | "command execution failure"
                        |  | "serialization failure"
                        |  | "runtime internal error";
                        |
                        |type PowerShellErrorPayload = {
                        |  category: PowerShellErrorCategory;
                        |  message: string;
                        |  sourceCommand: string;
                        |  details?: string;
                        |};
                        |
                        |function createRuntimeInternalError(
                        |  sourceCommand: string,
                        |  message: string,
                        |  details?: string,
                        |): PowerShellErrorPayload {
                        |  if (details !== undefined && details.length > 0) {
                        |    return { category: "runtime internal error", message, sourceCommand, details };
                        |  }
                        |
                        |  return { category: "runtime internal error", message, sourceCommand };
                        |}
                        |
                        |function isPowerShellErrorCategory(value: string): value is PowerShellErrorCategory {
                        |  return value === "invalid input"
                        |    || value === "module load failure"
                        |    || value === "bootstrap profile failure"
                        |    || value === "command execution failure"
                        |    || value === "serialization failure"
                        |    || value === "runtime internal error";
                        |}
                        |
                        |function isPowerShellErrorPayload(value: unknown): value is PowerShellErrorPayload {
                        |  if (typeof value !== "object" || value === null) {
                        |    return false;
                        |  }
                        |
                        |  const candidate = value as Record<string, unknown>;
                        |  return typeof candidate.category === "string"
                        |    && isPowerShellErrorCategory(candidate.category)
                        |    && typeof candidate.message === "string"
                        |    && typeof candidate.sourceCommand === "string"
                        |    && (candidate.details === undefined || typeof candidate.details === "string");
                        |}
                        |
                        |function parsePowerShellError(stderr: string, sourceCommand: string): PowerShellErrorPayload {
                        |  if (stderr.length === 0) {
                        |    return createRuntimeInternalError(
                        |      sourceCommand,
                        |      `PowerShell invocation for ${sourceCommand} failed without error output.`,
                        |    );
                        |  }
                        |
                        |  try {
                        |    const parsed = JSON.parse(stderr) as unknown;
                        |    if (isPowerShellErrorPayload(parsed)) {
                        |      return parsed;
                        |    }
                        |  }
                        |  catch {
                        |  }
                        |
                        |  return createRuntimeInternalError(
                        |    sourceCommand,
                        |    `PowerShell invocation for ${sourceCommand} failed without a structured error payload.`,
                        |    stderr,
                        |  );
                        |}
                        |
                        """));
        builder.AppendLine();
    }

    private static void RenderMain(StringBuilder builder)
    {
        builder.Append("""
            async function main(): Promise<void> {
              const transport = new StdioServerTransport();
              await server.connect(transport);
            }

            void main().catch((error) => {
              console.error(error);
              process.exitCode = 1;
            });
            """);
        builder.AppendLine();
    }

    private static async Task AppendObjectShapeAsync(
        StringBuilder builder,
        SchemaDefinition schema,
        int indentLevel,
        IReadOnlyDictionary<string, ParameterDefinition>? parameterLookup,
        CancellationToken cancellationToken)
    {
        var required = new HashSet<string>(schema.Required, StringComparer.Ordinal);

        foreach (var property in schema.Properties)
        {
            cancellationToken.ThrowIfCancellationRequested();

            builder.Append(new string(' ', indentLevel * IndentSize))
                .Append(property.Name)
                .Append(": ")
                .Append(await RenderPropertyAsync(
                    property,
                    required.Contains(property.Name),
                    indentLevel,
                    parameterLookup is not null && parameterLookup.TryGetValue(property.Name, out var parameter) ? parameter : null,
                    cancellationToken).ConfigureAwait(false))
                .AppendLine(",");
        }
    }

    private static async Task<string> RenderPropertyAsync(
        SchemaProperty property,
        bool isRequired,
        int indentLevel,
        ParameterDefinition? parameter,
        CancellationToken cancellationToken)
    {
        var expression = await RenderSchemaDefinitionAsync(
            CreatePropertySchemaDefinition(property),
            indentLevel,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        expression = await ApplyPropertyConstraintsAsync(expression, property, cancellationToken).ConfigureAwait(false);

        var description = CreateParameterDescription(parameter);
        if (description is not null)
        {
            expression += $".describe({QuoteTypeScriptString(description)})";
        }

        return isRequired ? expression : expression + ".optional()";
    }

    private static Task<string> ApplyPropertyConstraintsAsync(
        string baseExpression,
        SchemaProperty property,
        CancellationToken cancellationToken) => property.Type switch
        {
            "string" => ApplyStringConstraintsAsync(baseExpression, property.Enum, property.Pattern, cancellationToken),
            "integer" => Task.FromResult(ApplyNumericConstraints(baseExpression, property.Minimum, property.Maximum, property.Enum)),
            "number" => Task.FromResult(ApplyNumericConstraints(baseExpression, property.Minimum, property.Maximum, property.Enum)),
            _ => Task.FromResult(baseExpression),
        };

    private static string ApplyNumericConstraints(string baseExpression, string? minimum, string? maximum, ImmutableArray<string>? enumValues)
    {
        var hasEnum = enumValues is { Length: > 0 };

        if (hasEnum)
        {
            var literals = string.Join(", ", enumValues!.Value.Select(v => $"z.literal({QuoteNumericLiteral(v)})"));
            baseExpression = $"z.union([{literals}])";
        }

        if (hasEnum)
        {
            return baseExpression;
        }

        return AppendNumericConstraints(baseExpression, minimum, maximum);
    }

    private static string QuoteNumericLiteral(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? value
            : QuoteTypeScriptString(value);

    private static async Task<string> ApplyStringConstraintsAsync(
        string baseExpression,
        ImmutableArray<string>? enumValues,
        string? pattern,
        CancellationToken cancellationToken)
    {
        var hasEnum = enumValues is { Length: > 0 };
        var enumEntries = enumValues.GetValueOrDefault();
        var expression = hasEnum
            ? $"z.enum([{string.Join(", ", enumEntries.Select(QuoteTypeScriptString))}])"
            : baseExpression;

        if (pattern is null)
        {
            return expression;
        }

        await ValidateRegexPatternAsync(pattern, cancellationToken).ConfigureAwait(false);

        if (!hasEnum)
        {
            return expression + $".regex(new RegExp({QuoteTypeScriptString(pattern)}))";
        }

        return expression
            + $".refine((value) => new RegExp({QuoteTypeScriptString(pattern)}).test(value), "
            + $"{{ message: {QuoteTypeScriptString($"Expected value matching pattern {pattern}.")} }})";
    }

    private static async Task ValidateRegexPatternAsync(string pattern, CancellationToken cancellationToken)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.ECMAScript | RegexOptions.Compiled);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"Invalid regular expression pattern '{pattern}': {ex.Message}", ex);
        }

        await ValidateRegexSafetyAsync(pattern, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ValidateRegexSafetyAsync(string pattern, CancellationToken cancellationToken)
    {
        var regex = new Regex(pattern, RegexOptions.ECMAScript | RegexOptions.Compiled);
        var timeout = TimeSpan.FromMilliseconds(100);
        var rejectionMessage = $"Regex pattern '{pattern}' is potentially vulnerable to catastrophic backtracking and was rejected.";
        var adversarialInputs = new[]
        {
            new string('a', 25),
            new string('a', 25) + "!",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaa!",
        };

        foreach (var input in adversarialInputs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                var task = Task.Run(() => regex.IsMatch(input), cts.Token);
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token)).ConfigureAwait(false);
                if (completedTask != task)
                {
                    cts.Cancel();
                    throw new InvalidOperationException(rejectionMessage);
                }

                _ = await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(rejectionMessage);
            }
        }
    }

    /// <summary>
    /// Normalizes property-attached schema payloads into the structural shape expected by <see cref="RenderSchemaDefinition"/>.
    /// Array properties can arrive either as the item schema directly or as an array schema whose <c>Items</c> contains the item schema.
    /// </summary>
    private static SchemaDefinition CreatePropertySchemaDefinition(SchemaProperty property)
    {
        if (property.Schema is null)
        {
            return new SchemaDefinition(property.Type, [], [], null);
        }

        if (string.Equals(property.Type, "array", StringComparison.Ordinal))
        {
            var items = string.Equals(property.Schema.Type, "array", StringComparison.Ordinal)
                ? property.Schema.Items
                : property.Schema;

            return new SchemaDefinition(property.Type, [], [], items, property.Schema.ComplexType);
        }

        return new SchemaDefinition(
            property.Type,
            property.Schema.Properties,
            property.Schema.Required,
            property.Schema.Items,
            property.Schema.ComplexType);
    }

    /// <summary>
    /// Renders an array item schema from either the normalized <c>Items</c> payload or a direct item schema.
    /// The direct-item form is retained to tolerate older IR shapes while property schemas are normalized locally.
    /// </summary>
    private static async Task<string> RenderArrayElementSchemaAsync(
        SchemaDefinition? schema,
        int indentLevel,
        CancellationToken cancellationToken)
    {
        if (schema is null)
        {
            return "z.unknown()";
        }

        if (string.Equals(schema.Type, "array", StringComparison.Ordinal))
        {
            return schema.Items is null
                ? "z.unknown()"
                : await RenderSchemaDefinitionAsync(schema.Items, indentLevel, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return await RenderSchemaDefinitionAsync(schema, indentLevel, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, string> CreateSchemaIdentifiers(ImmutableArray<ToolDefinition> tools)
    {
        var allocated = new HashSet<string>(StringComparer.Ordinal);
        var identifiers = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 0; index < tools.Length; index++)
        {
            var tool = tools[index];
            var baseIdentifier = GetSchemaIdentifierBase(tool.ToolName);
            var identifier = baseIdentifier;
            var suffix = 2;

            while (!allocated.Add(identifier))
            {
                identifier = baseIdentifier + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }

            identifiers.Add(tool.ToolName, identifier);
        }

        return identifiers;
    }

    private static async Task<string> RenderSchemaDefinitionAsync(
        SchemaDefinition schema,
        int indentLevel,
        IReadOnlyDictionary<string, ParameterDefinition>? parameterLookup = null,
        CancellationToken cancellationToken = default)
    {
        return schema.Type switch
        {
            "string" => "z.string()",
            "integer" => "z.number().int()",
            "number" => "z.number()",
            "boolean" => "z.boolean()",
            "array" => $"z.array({await RenderArrayElementSchemaAsync(schema, indentLevel, cancellationToken).ConfigureAwait(false)})",
            "object" => await RenderObjectSchemaAsync(schema, indentLevel + 1, parameterLookup, cancellationToken).ConfigureAwait(false),
            _ => "z.unknown()",
        };
    }

    private static async Task<string> RenderObjectSchemaAsync(
        SchemaDefinition? schema,
        int indentLevel,
        IReadOnlyDictionary<string, ParameterDefinition>? parameterLookup,
        CancellationToken cancellationToken)
    {
        if (schema is null)
        {
            return "z.object({})";
        }

        if (schema.Properties.Length == 0)
        {
            return "z.object({})";
        }

        var indent = new string(' ', indentLevel * IndentSize);
        var closingIndent = new string(' ', (indentLevel - 1) * IndentSize);
        var builder = new StringBuilder();
        builder.AppendLine("z.object({");

        await AppendObjectShapeAsync(builder, schema, indentLevel, parameterLookup, cancellationToken).ConfigureAwait(false);

        builder.Append(closingIndent).Append("})");
        return builder.ToString();
    }

    private static IReadOnlyDictionary<string, ParameterDefinition> CreateParameterLookup(ImmutableArray<ParameterDefinition> parameters)
    {
        var lookup = new Dictionary<string, ParameterDefinition>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            lookup[parameter.Name] = parameter;
        }

        return lookup;
    }

    private static string? CreateParameterDescription(ParameterDefinition? parameter)
    {
        if (parameter is null)
        {
            return null;
        }

        if (parameter.IsSecure)
        {
            return string.IsNullOrWhiteSpace(parameter.Description)
                ? "Treated as a secret."
                : parameter.Description + " Treated as a secret.";
        }

        return string.IsNullOrWhiteSpace(parameter.Description) ? null : parameter.Description;
    }

    private static IEnumerable<string> GetSecureStringParameterNames(ImmutableArray<ParameterDefinition> parameters) =>
        parameters
            .Where(static parameter => parameter.IsSecure && IsSecureStringParameterType(parameter.Type))
            .Select(static parameter => parameter.Name);

    private static bool IsSecureStringParameterType(string parameterType)
    {
        var normalized = parameterType.Trim();
        if (normalized.EndsWith("[]", StringComparison.Ordinal))
        {
            normalized = normalized[..^2];
        }

        const string namespacePrefix = "System.Security.";
        if (normalized.StartsWith(namespacePrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[namespacePrefix.Length..];
        }

        return string.Equals(normalized, "SecureString", StringComparison.OrdinalIgnoreCase);
    }

    private static string RenderTypeScriptStringArray(IEnumerable<string> values) =>
        $"[{string.Join(", ", values.Select(QuoteTypeScriptString))}]";

    private static string AppendNumericConstraints(string baseExpression, string? minimum, string? maximum)
    {
        var expression = baseExpression;
        if (minimum is not null)
        {
            expression += $".min({FormatNumeric(minimum)})";
        }

        if (maximum is not null)
        {
            expression += $".max({FormatNumeric(maximum)})";
        }

        return expression;
    }

    private static string FormatNumeric(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed.ToString(CultureInfo.InvariantCulture);
        }

        throw new ArgumentException($"Invalid numeric constraint value: '{value}'.");
    }

    internal static string GetSchemaIdentifierBase(string toolName)
    {
        var segments = toolName.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return "toolInputSchema";
        }

        var builder = new StringBuilder();
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            var normalized = char.ToUpperInvariant(segment[0]) + segment[1..].ToLower(CultureInfo.InvariantCulture);
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

        builder.Append("InputSchema");
        return builder.ToString();
    }

    private static string QuoteTypeScriptString(string value)
    {
        var encoded = JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
        return "\"" + encoded.ToString() + "\"";
    }

    private static string StripMargin(string value)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var marginIndex = lines[index].IndexOf('|', StringComparison.Ordinal);
            if (marginIndex >= 0)
            {
                lines[index] = lines[index][(marginIndex + 1)..];
            }
        }

        return string.Join("\n", lines);
    }

    private static string WriteJson(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            write(json);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static string ResolveModuleVersion(string? version) => IsValidSemVer(version) ? version! : DefaultPackageVersion;

    private static string GetPackageName(string moduleName)
    {
        var builder = new StringBuilder(moduleName.Length + 11);
        var wroteSeparator = false;

        foreach (var ch in moduleName)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                wroteSeparator = false;
                continue;
            }

            if (!wroteSeparator)
            {
                builder.Append('-');
                wroteSeparator = true;
            }
        }

        var sanitized = builder.ToString().Trim('-');
        if (sanitized.Length == 0)
        {
            return DefaultPackageName;
        }

        var candidate = sanitized + "-mcp-server";
        return IsValidNpmPackageName(candidate) ? candidate : DefaultPackageName;
    }

    private static bool IsValidSemVer(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var buildSeparatorIndex = value.IndexOf('+', StringComparison.Ordinal);
        if (buildSeparatorIndex >= 0 && value.IndexOf('+', buildSeparatorIndex + 1) >= 0)
        {
            return false;
        }

        var mainAndBuild = SplitAtFirst(value, buildSeparatorIndex);
        var prereleaseSeparatorIndex = mainAndBuild.Before.IndexOf('-', StringComparison.Ordinal);
        var mainAndPrerelease = SplitAtFirst(mainAndBuild.Before, prereleaseSeparatorIndex);

        var coreIdentifiers = mainAndPrerelease.Before.Split('.');
        if (coreIdentifiers.Length != 3 || coreIdentifiers.Any(identifier => !IsNumericSemVerIdentifier(identifier)))
        {
            return false;
        }

        if (mainAndPrerelease.After is not null && !HasValidSemVerIdentifiers(mainAndPrerelease.After, allowLeadingZeroesInNumericIdentifiers: false))
        {
            return false;
        }

        if (mainAndBuild.After is not null && !HasValidSemVerIdentifiers(mainAndBuild.After, allowLeadingZeroesInNumericIdentifiers: true))
        {
            return false;
        }

        return true;
    }

    private static (string Before, string? After) SplitAtFirst(string value, int separatorIndex)
    {
        if (separatorIndex < 0)
        {
            return (value, null);
        }

        return (value[..separatorIndex], value[(separatorIndex + 1)..]);
    }

    private static bool HasValidSemVerIdentifiers(string value, bool allowLeadingZeroesInNumericIdentifiers)
    {
        var identifiers = value.Split('.');
        return identifiers.Length > 0 && identifiers.All(identifier => IsValidSemVerIdentifier(identifier, allowLeadingZeroesInNumericIdentifiers));
    }

    private static bool IsValidSemVerIdentifier(string identifier, bool allowLeadingZeroesInNumericIdentifiers)
    {
        if (identifier.Length == 0 || identifier.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '-'))
        {
            return false;
        }

        if (!char.IsDigit(identifier[0]))
        {
            return true;
        }

        if (identifier.Any(ch => !char.IsDigit(ch)))
        {
            return true;
        }

        return allowLeadingZeroesInNumericIdentifiers || identifier.Length == 1 || identifier[0] != '0';
    }

    private static bool IsNumericSemVerIdentifier(string identifier)
    {
        if (identifier.Length == 0 || identifier.Any(ch => !char.IsDigit(ch)))
        {
            return false;
        }

        return identifier.Length == 1 || identifier[0] != '0';
    }

    private static bool IsValidNpmPackageName(string value)
    {
        if (value.Length is 0 or > 214)
        {
            return false;
        }

        if (value[0] is '.' or '_')
        {
            return false;
        }

        return Regex.IsMatch(value, "^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}

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

    /// <inheritdoc />
    public Task<EmitResult> EmitAsync(
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
        var indexFile = new EmittedFile(IndexFilePath, RenderIndex(server, options, cancellationToken));
        packageJsonFile.Validate();
        tsConfigFile.Validate();
        indexFile.Validate();

        return Task.FromResult(new EmitResult(ImmutableArray.Create(packageJsonFile, tsConfigFile, indexFile)));
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

    private static string RenderIndex(McpServerDefinition server, EmitOptions options, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var schemaIdentifiers = CreateSchemaIdentifiers(server.Tools);
        var resolvedVersion = ResolveModuleVersion(server.Module.Version);

        builder.AppendLine("import { McpServer } from \"@modelcontextprotocol/sdk/server/mcp.js\";");
        builder.AppendLine("import { StdioServerTransport } from \"@modelcontextprotocol/sdk/server/stdio.js\";");
        builder.AppendLine("import { z } from \"zod\";");
        builder.AppendLine();
        builder.Append("const bundledModuleImportPath = ")
            .Append(QuoteTypeScriptString(options.BundledModuleImportPath))
            .AppendLine(";");
        builder.AppendLine();

        foreach (var tool in server.Tools)
        {
            cancellationToken.ThrowIfCancellationRequested();

            builder.Append("const ")
                .Append(schemaIdentifiers[tool.ToolName])
                .Append(" = ")
                .Append(RenderSchemaDefinition(tool.Schema, 0))
                .AppendLine(";");
            builder.AppendLine();
        }

        builder.AppendLine("const server = new McpServer({");
        builder.Append("  name: ")
            .Append(QuoteTypeScriptString(server.Module.Name))
            .AppendLine(",");
        builder.Append("  version: ")
            .Append(QuoteTypeScriptString(resolvedVersion))
            .AppendLine(",");
        builder.AppendLine("});");
        builder.AppendLine();

        foreach (var tool in server.Tools)
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
                .Append(tool.Execution.SerializationDepth.ToString(CultureInfo.InvariantCulture))
                .AppendLine("),");
            builder.AppendLine(");");
            builder.AppendLine();
        }

        builder.AppendLine("async function invokePowerShellTool(");
        builder.AppendLine("  sourceCommand: string,");
        builder.AppendLine("  _args: unknown,");
        builder.AppendLine("  serializationDepth: number,");
        builder.AppendLine("): Promise<{ content: Array<{ type: \"text\"; text: string }> }> {");
        builder.AppendLine("  // TODO: Implement PowerShell invocation once a cross-platform spawn mechanism is defined.");
        builder.AppendLine("  throw new Error(");
        builder.AppendLine("    `PowerShell invocation for ${sourceCommand} is not implemented yet. Bundled module path: ${bundledModuleImportPath}. Serialization depth: ${serializationDepth}.`,");
        builder.AppendLine("  );");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("async function main(): Promise<void> {");
        builder.AppendLine("  const transport = new StdioServerTransport();");
        builder.AppendLine("  await server.connect(transport);");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("void main().catch((error) => {");
        builder.AppendLine("  console.error(error);");
        builder.AppendLine("  process.exitCode = 1;");
        builder.AppendLine("});");

        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void AppendObjectShape(StringBuilder builder, SchemaDefinition schema, int indentLevel)
    {
        var required = new HashSet<string>(schema.Required, StringComparer.Ordinal);

        foreach (var property in schema.Properties)
        {
            builder.Append(new string(' ', indentLevel * IndentSize))
                .Append(property.Name)
                .Append(": ")
                .Append(RenderProperty(property, required.Contains(property.Name), indentLevel))
                .AppendLine(",");
        }
    }

    private static string RenderProperty(SchemaProperty property, bool isRequired, int indentLevel)
    {
        var expression = RenderSchemaDefinition(CreatePropertySchemaDefinition(property), indentLevel);
        expression = ApplyPropertyConstraints(expression, property);
        return isRequired ? expression : expression + ".optional()";
    }

    private static string ApplyPropertyConstraints(string baseExpression, SchemaProperty property) => property.Type switch
    {
        "string" => ApplyStringConstraints(baseExpression, property.Enum, property.Pattern),
        "integer" => AppendNumericConstraints(baseExpression, property.Minimum, property.Maximum),
        "number" => AppendNumericConstraints(baseExpression, property.Minimum, property.Maximum),
        _ => baseExpression,
    };

    private static string ApplyStringConstraints(string baseExpression, ImmutableArray<string>? enumValues, string? pattern)
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

        if (!hasEnum)
        {
            return expression + $".regex(new RegExp({QuoteTypeScriptString(pattern)}))";
        }

        return expression
            + $".refine((value) => new RegExp({QuoteTypeScriptString(pattern)}).test(value), "
            + $"{{ message: {QuoteTypeScriptString($"Expected value matching pattern {pattern}.")} }})";
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
    private static string RenderArrayElementSchema(SchemaDefinition? schema, int indentLevel)
    {
        if (schema is null)
        {
            return "z.unknown()";
        }

        if (string.Equals(schema.Type, "array", StringComparison.Ordinal))
        {
            return schema.Items is null ? "z.unknown()" : RenderSchemaDefinition(schema.Items, indentLevel);
        }

        return RenderSchemaDefinition(schema, indentLevel);
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

    private static string RenderSchemaDefinition(SchemaDefinition schema, int indentLevel) => schema.Type switch
    {
        "string" => "z.string()",
        "integer" => "z.number().int()",
        "number" => "z.number()",
        "boolean" => "z.boolean()",
        "array" => $"z.array({RenderArrayElementSchema(schema, indentLevel)})",
        "object" => RenderObjectSchema(schema, indentLevel + 1),
        _ => "z.unknown()",
    };

    private static string RenderObjectSchema(SchemaDefinition? schema, int indentLevel)
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

        AppendObjectShape(builder, schema, indentLevel);

        builder.Append(closingIndent).Append("})");
        return builder.ToString();
    }

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

    private static string GetSchemaIdentifierBase(string toolName)
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

        var buildSplit = value.Split('+');
        if (buildSplit.Length > 2)
        {
            return false;
        }

        var mainAndPrerelease = buildSplit[0].Split('-');
        if (mainAndPrerelease.Length > 2)
        {
            return false;
        }

        var coreIdentifiers = mainAndPrerelease[0].Split('.');
        if (coreIdentifiers.Length != 3 || coreIdentifiers.Any(identifier => !IsNumericSemVerIdentifier(identifier)))
        {
            return false;
        }

        if (mainAndPrerelease.Length == 2 && !HasValidSemVerIdentifiers(mainAndPrerelease[1], allowLeadingZeroesInNumericIdentifiers: false))
        {
            return false;
        }

        if (buildSplit.Length == 2 && !HasValidSemVerIdentifiers(buildSplit[1], allowLeadingZeroesInNumericIdentifiers: true))
        {
            return false;
        }

        return true;
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

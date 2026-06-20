using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
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

        var emittedFile = new EmittedFile(IndexFilePath, RenderIndex(server, options, cancellationToken));
        emittedFile.Validate();

        return Task.FromResult(new EmitResult(ImmutableArray.Create(emittedFile)));
    }

    private static string RenderIndex(McpServerDefinition server, EmitOptions options, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var schemaIdentifiers = CreateSchemaIdentifiers(server.Tools);

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
                .AppendLine(" = z.object({");

            AppendObjectShape(builder, tool.Schema, 1);
            builder.AppendLine("});");
            builder.AppendLine();
        }

        builder.AppendLine("const server = new McpServer({");
        builder.Append("  name: ")
            .Append(QuoteTypeScriptString(server.Module.Name))
            .AppendLine(",");
        if (server.Module.Version is not null)
        {
            builder.Append("  version: ")
                .Append(QuoteTypeScriptString(server.Module.Version))
                .AppendLine(",");
        }
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
            builder.Append("    title: ")
                .Append(QuoteTypeScriptString(tool.SourceCommand))
                .AppendLine(",");
            builder.Append("    description: ")
                .Append(QuoteTypeScriptString(tool.Description))
                .AppendLine(",");
            builder.Append("    inputSchema: ")
                .Append(schemaIdentifiers[tool.ToolName])
                .AppendLine(".shape,");
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
        builder.AppendLine("  args: unknown,");
        builder.AppendLine("  serializationDepth: number,");
        builder.AppendLine("): Promise<{ content: Array<{ type: \"text\"; text: string }> }> {");
        builder.AppendLine("  void args;");
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
            builder.Append(new string(' ', indentLevel * 2))
                .Append(property.Name)
                .Append(": ")
                .Append(RenderProperty(property, required.Contains(property.Name), indentLevel))
                .AppendLine(",");
        }
    }

    private static string RenderProperty(SchemaProperty property, bool isRequired, int indentLevel)
    {
        var expression = RenderZodExpression(property.Type, property.Enum, property.Minimum, property.Maximum, property.Pattern, property.Schema, indentLevel);
        return isRequired ? expression : expression + ".optional()";
    }

    private static string RenderZodExpression(
        string type,
        ImmutableArray<string>? enumValues,
        string? minimum,
        string? maximum,
        string? pattern,
        SchemaDefinition? schema,
        int indentLevel)
    {
        var expression = type switch
        {
            "string" => RenderStringExpression(enumValues, pattern),
            "integer" => AppendNumericConstraints("z.number().int()", minimum, maximum),
            "number" => AppendNumericConstraints("z.number()", minimum, maximum),
            "boolean" => "z.boolean()",
            "array" => $"z.array({RenderArrayElementSchema(schema, indentLevel)})",
            "object" => RenderObjectSchema(schema, indentLevel + 1),
            _ => "z.unknown()",
        };

        return expression;
    }

    private static string RenderStringExpression(ImmutableArray<string>? enumValues, string? pattern)
    {
        var hasEnum = enumValues is { Length: > 0 };
        var enumEntries = enumValues.GetValueOrDefault();
        var expression = hasEnum
            ? $"z.enum([{string.Join(", ", enumEntries.Select(QuoteTypeScriptString))}])"
            : "z.string()";

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

        var indent = new string(' ', indentLevel * 2);
        var closingIndent = new string(' ', (indentLevel - 1) * 2);
        var required = new HashSet<string>(schema.Required, StringComparer.Ordinal);
        var builder = new StringBuilder();
        builder.Append("z.object({\n");

        foreach (var property in schema.Properties)
        {
            builder.Append(indent)
                .Append(property.Name)
                .Append(": ")
                .Append(RenderProperty(property, required.Contains(property.Name), indentLevel))
                .Append(",\n");
        }

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
}

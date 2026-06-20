using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Management.Automation.Language;
using Ps2Mcp.Core;

namespace Ps2Mcp.Introspection;

/// <summary>
/// Walks a parsed script module AST and produces a <see cref="McpServerDefinition"/> IR
/// containing one <see cref="ToolDefinition"/> per top-level function.
/// </summary>
/// <remarks>
/// Script-module introspection is the in-process, AOT-safe path for analyzing PowerShell
/// script modules (.psm1) by static AST inspection; see §13.1 of the project specification.
/// Binary modules are out of scope here and are handled by a separate component (Phase 6).
/// <para>
/// The output is intentionally a "partial" IR: each tool is fully described at the
/// PowerShell level (parameters, types, validation attributes, help text, output type
/// hints), but the schema mapping from PowerShell types to JSON Schema types is left to
/// the schema mapper (Phase 8). The current implementation stores the raw PowerShell
/// type name in <see cref="SchemaProperty.Type"/>; consumers that need JSON Schema
/// types should run the schema mapper on the output.
/// </para>
/// <para>
/// Only top-level function definitions are exposed as tools; nested functions inside a
/// function's body are intentionally skipped because they are conventionally private
/// helpers in PowerShell modules. Functions defined inside top-level control-flow
/// blocks (e.g. <c>if</c> / <c>switch</c>) are also excluded because
/// <c>searchNestedScriptBlocks: false</c> skips all nested script blocks, not just
/// those inside function bodies. The .psm1 file's basename (without extension) is
/// used as the module name; explicit override of the module identity is left to the
/// orchestrator (Phase 7).
/// </para>
/// <para>
/// <see cref="ScriptModuleParseResult.HasErrors"/> is not a fatal signal here. The
/// introspector extracts whatever tools it can from whatever AST the parser produced and
/// returns a result. The orchestrator decides whether to fail the build on parse errors.
/// </para>
/// </remarks>
public static class ScriptModuleIntrospector
{

    /// <summary>
    /// Builds a <see cref="McpServerDefinition"/> from a parsed script module AST.
    /// </summary>
    /// <param name="parseResult">The parsed module produced by <see cref="ScriptModuleParser.Parse"/>.</param>
    /// <returns>An IR containing one tool per top-level function. Returns an empty
    /// (no-tools) server definition if the AST contains no top-level functions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parseResult"/> is <c>null</c>.</exception>
    public static McpServerDefinition Introspect(ScriptModuleParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        var moduleName = Path.GetFileNameWithoutExtension(parseResult.FilePath);
        var module = new ModuleDefinition(moduleName, Version: null);

        // Ast is guaranteed non-null: Parser.ParseFile always returns a partial tree,
        // and ScriptModuleParseResult.Ast is a non-nullable record parameter.
        // searchNestedScriptBlocks: false skips nested functions (private helpers) and
        // functions inside top-level control-flow blocks (if/switch), exposing only
        // unconditionally defined top-level functions as tools.
        var functions = parseResult.Ast
            .FindAll(a => a is FunctionDefinitionAst, searchNestedScriptBlocks: false)
            .Cast<FunctionDefinitionAst>()
            .ToImmutableArray();

        var toolBuilder = ImmutableArray.CreateBuilder<ToolDefinition>(functions.Length);
        foreach (var function in functions)
        {
            toolBuilder.Add(IntrospectFunction(function, parseResult.Tokens));
        }

        return new McpServerDefinition(module, toolBuilder.ToImmutable());
    }

    private static ToolDefinition IntrospectFunction(FunctionDefinitionAst function, ImmutableArray<Token> tokens)
    {
        var help = CommandHelpExtractor.Extract(function);
        var extractions = ExtractParameters(function, help);
        var output = ExtractOutputType(function);
        var schema = BuildSchema(extractions);

        var description = GetDescription(help);

        var parameters = extractions.Select(e => e.Definition).ToImmutableArray();
        var requiredSet = ExtractDefaultParameterSet(function, tokens);

        return new ToolDefinition(
            ToolName: function.Name,
            SourceCommand: function.Name,
            Description: description,
            Parameters: parameters,
            RequiredParameterSet: requiredSet,
            Schema: schema,
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth),
            Help: help is null ? null : ToHelpMetadata(help),
            Output: output);
    }

    private readonly record struct ParameterExtraction(
        ParameterDefinition Definition,
        ParameterAttributeInfo Attributes);

    // The PowerShell parser consumes [CmdletBinding()] into internal state that is not exposed
    // via the public AST API in SDK 7.6.2 (ScriptRequirements is null, Body.Attributes is null).
    // The only way to extract DefaultParameterSetName is from the token stream: look for the
    // 'CmdletBinding' token before the function, then scan forward for 'DefaultParameterSetName ='
    // followed by a string literal. We find the CmdletBinding closest to (but before) the
    // function to avoid picking up a different function's [CmdletBinding()].
    private static string? ExtractDefaultParameterSet(
        FunctionDefinitionAst function,
        ImmutableArray<Token> tokens)
    {
        if (tokens.IsDefaultOrEmpty)
        {
            return null;
        }

        var functionStart = function.Extent.StartOffset;

        // Find the CmdletBinding token closest to (but before) this function.
        var cmdletBindingIndex = -1;
        for (var i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].Extent.StartOffset >= functionStart)
            {
                break;
            }

            if (string.Equals(tokens[i].Text, "CmdletBinding", StringComparison.OrdinalIgnoreCase))
            {
                cmdletBindingIndex = i;
            }
        }

        if (cmdletBindingIndex < 0)
        {
            return null;
        }

        // Scan forward from CmdletBinding for DefaultParameterSetName = '<value>'.
        for (var j = cmdletBindingIndex + 1; j < tokens.Length && j < cmdletBindingIndex + 10; j++)
        {
            if (!string.Equals(tokens[j].Text, "DefaultParameterSetName", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Next token should be '='.
            if (j + 1 < tokens.Length && tokens[j + 1].Kind == TokenKind.Equals)
            {
                // Token after '=' should be a string literal.
                if (j + 2 < tokens.Length && tokens[j + 2] is StringLiteralToken setString)
                {
                    return setString.Value;
                }
            }

            break;
        }

        return null;
    }

    private static ImmutableArray<ParameterExtraction> ExtractParameters(
        FunctionDefinitionAst function,
        CommandHelpInfo? help)
    {
        var paramBlock = function.Body?.ParamBlock;
        if (paramBlock is null || paramBlock.Parameters.Count == 0)
        {
            return ImmutableArray<ParameterExtraction>.Empty;
        }

        Dictionary<string, CommandHelpInfo.ParameterHelp>? helpByName = null;
        if (help is not null && !help.Parameters.IsDefaultOrEmpty)
        {
            helpByName = new Dictionary<string, CommandHelpInfo.ParameterHelp>(
                help.Parameters.Length,
                StringComparer.OrdinalIgnoreCase);
            foreach (var entry in help.Parameters)
            {
                helpByName[entry.Name] = entry;
            }
        }

        var builder = ImmutableArray.CreateBuilder<ParameterExtraction>(paramBlock.Parameters.Count);
        foreach (var parameter in paramBlock.Parameters)
        {
            var attributes = ParameterAttributeExtractor.Extract(parameter);
            var name = parameter.Name.VariablePath.UserPath;
            string? description = null;
            if (helpByName is not null && helpByName.TryGetValue(name, out var helpEntry))
            {
                description = helpEntry.Description;
            }
            var defaultValue = ExtractDefaultValue(parameter);
            var isSecure = PowerShellTypeMapper.IsSecureType(attributes.Type);

            var definition = new ParameterDefinition(
                Name: name,
                Type: attributes.Type,
                IsMandatory: attributes.IsMandatory,
                IsSecure: isSecure,
                Description: description,
                DefaultValue: defaultValue,
                Aliases: attributes.Aliases,
                ParameterSets: attributes.ParameterSets);

            builder.Add(new ParameterExtraction(definition, attributes));
        }

        return builder.ToImmutable();
    }

    // Literal values (string, number, boolean) are unwrapped from the boxed .NET value rather
    // than read as source text, so the consumer sees the evaluated value (e.g. the int 5
    // becomes "5", the string 'foo' becomes "foo" without quotes). Variable references are
    // surfaced as the variable's name (e.g. "foo" for $foo) so the consumer can decide
    // how to bind them. Array literals (@() or @('a','b')) are surfaced as the source text,
    // which preserves the PowerShell syntax verbatim. Other expression shapes (method calls,
    // hashtables, casts) are left null to avoid fabricating a representation the consumer
    // cannot parse.
    private static string? ExtractDefaultValue(ParameterAst parameter)
    {
        if (parameter.DefaultValue is null)
        {
            return null;
        }
        return parameter.DefaultValue switch
        {
            ConstantExpressionAst { Value: not null } constant => constant.Value switch
            {
                IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => constant.Value.ToString(),
            },
            VariableExpressionAst variable => variable.VariablePath.UserPath,
            // The PowerShell parser uses ArrayExpressionAst (the comma-operator AST) for
            // both the explicit @(item1, item2) sub-expression form and the bare
            // `,`-list form in default-value position. ArrayLiteralAst is matched too
            // for parser versions that may emit it; both yield their full Extent.Text
            // (preserving source quoting and the leading `@` if present).
            ArrayLiteralAst or ArrayExpressionAst => parameter.DefaultValue.Extent.Text,
            _ => null,
        };
    }

    private static SchemaDefinition BuildSchema(ImmutableArray<ParameterExtraction> extractions)
    {
        if (extractions.IsDefaultOrEmpty)
        {
            return new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray<SchemaProperty>.Empty,
                Required: ImmutableArray<string>.Empty,
                Items: null);
        }

        var parameters = extractions.Select(e => e.Definition).ToImmutableArray();
        var validationByParam = new Dictionary<ParameterDefinition, ValidationMapping>(
            extractions.Length, ReferenceEqualityComparer.Instance);
        foreach (var extraction in extractions)
        {
            validationByParam[extraction.Definition] = ValidationMapper.Map(extraction.Attributes);
        }

        return SchemaBuilder.FromParameters(parameters, def => validationByParam[def]);
    }

    private static OutputMetadata? ExtractOutputType(FunctionDefinitionAst function)
    {
        var outputTypeAttr = FindOutputTypeAttribute(function.Body);
        if (outputTypeAttr is null)
        {
            return null;
        }

        // The IR currently carries a single output type, so only the first declared
        // [OutputType] in source order is preserved here.
        var typeName = ExtractOutputTypeName(outputTypeAttr);
        return typeName is null ? null : new OutputMetadata(typeName, OutputTypeArguments: null);
    }

    // [OutputType(...)] can appear in several legal placements inside a function body: as an
    // attribute on the param block, as an AttributedExpressionAst wrapping a body expression,
    // or as a bare attribute on a body statement (without an associated expression). A single
    // FindAll over the body's top level catches all of these forms. searchNestedScriptBlocks
    // is set to false so we do not descend into nested script blocks (e.g. `& { ... }`,
    // `Where-Object { ... }`, or nested function definitions) — those would be a different
    // function's [OutputType], not the one being introspected.
    private static AttributeAst? FindOutputTypeAttribute(ScriptBlockAst? body)
    {
        if (body is null)
        {
            return null;
        }

        return body.FindAll(a =>
            a is AttributeAst attr &&
            ParameterAttributeExtractor.IsAttributeNamed(attr, "OutputType"),
            searchNestedScriptBlocks: false)
            .Cast<AttributeAst>()
            .OrderBy(static attr => attr.Extent.StartOffset)
            .FirstOrDefault();
    }

    private static string? ExtractOutputTypeName(ExpressionAst arg) =>
        arg switch
        {
            StringConstantExpressionAst str => str.Value,
            TypeExpressionAst typeExpr when typeExpr.TypeName is not null => typeExpr.TypeName.Name,
            _ => null,
        };

    private static string? ExtractOutputTypeName(AttributeAst outputTypeAttr)
    {
        if (outputTypeAttr.PositionalArguments is { Count: > 0 })
        {
            return ExtractOutputTypeName(outputTypeAttr.PositionalArguments[0]);
        }

        var namedTypeName = outputTypeAttr.NamedArguments?
            .FirstOrDefault(static arg =>
                string.Equals(arg.ArgumentName, "TypeName", StringComparison.OrdinalIgnoreCase));

        return namedTypeName is null ? null : ExtractOutputTypeName(namedTypeName.Argument);
    }

    private static string GetDescription(CommandHelpInfo? help) =>
        !string.IsNullOrWhiteSpace(help?.Synopsis) ? help!.Synopsis
        : !string.IsNullOrWhiteSpace(help?.Description) ? help!.Description
        : string.Empty;

    private static HelpMetadata ToHelpMetadata(CommandHelpInfo help)
    {
        var exampleBuilder = ImmutableArray.CreateBuilder<HelpExample>(help.Examples.Length);
        foreach (var code in help.Examples)
        {
            exampleBuilder.Add(new HelpExample(Title: null, Code: code, Remarks: null));
        }
        return new HelpMetadata(
            Synopsis: help.Synopsis,
            Description: help.Description,
            Examples: exampleBuilder.ToImmutable());
    }
}

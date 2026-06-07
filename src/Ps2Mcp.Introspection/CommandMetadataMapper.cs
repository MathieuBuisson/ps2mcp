using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ps2Mcp.Core;

namespace Ps2Mcp.Introspection;

// Converts a JSON deserialized binary-introspection payload into the project IR.
// The mapping is the binary-module analog of ScriptModuleIntrospector: a one-shot
// pass per command that builds a ToolDefinition, then a SchemaDefinition that
// reflects the parameter shape. The payload carries only what PowerShell's
// CommandMetadata exposes (parameter name, type, isMandatory, position, value
// pipeline flags, aliases, parameter sets, output types); fields PowerShell does
// not surface (validate-set, validate-range, validate-pattern, default value,
// parameter description) are intentionally left null/empty here. Phase 8's schema
// mapper takes the partial output and produces the full JSON-Schema-typed shape.
internal static class CommandMetadataMapper
{
    private const int DefaultSerializationDepth = 4;

    // Map(BinaryIntrospectionPayload) → McpServerDefinition.
    // The version field is not part of the binary payload (PowerShell does not
    // surface the module's manifest version via Get-Command's metadata), so the
    // module definition carries Version=null and lets the orchestrator override
    // it from the manifest when available.
    public static McpServerDefinition Map(BinaryIntrospectionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var module = new ModuleDefinition(payload.ModuleName, Version: null);
        var tools = payload.Commands
            .Select(MapCommand)
            .ToImmutableArray();
        return new McpServerDefinition(module, tools);
    }

    private static ToolDefinition MapCommand(BinaryCommandPayload command)
    {
        var parameters = command.Parameters
            .Select(MapParameter)
            .ToImmutableArray();
        var schema = BuildSchema(parameters);
        var output = MapOutput(command.OutputType);
        var requiredSet = string.IsNullOrEmpty(command.DefaultParameterSetName)
            ? null
            : command.DefaultParameterSetName;

        return new ToolDefinition(
            ToolName: command.Name,
            SourceCommand: command.Name,
            Description: string.Empty,
            Parameters: parameters,
            RequiredParameterSet: requiredSet,
            Schema: schema,
            Execution: new ExecutionDefinition(DefaultSerializationDepth),
            Help: null,
            Output: output);
    }

    private static ParameterDefinition MapParameter(BinaryParameterPayload parameter)
    {
        var humanizedType = TypeNameHumanizer.Humanize(parameter.Type);
        var isSecure = IsSecureType(humanizedType);
        var aliases = parameter.Aliases is { Count: > 0 }
            ? parameter.Aliases.ToImmutableArray()
            : ImmutableArray<string>.Empty;
        var parameterSets = parameter.ParameterSets is { Count: > 0 }
            ? parameter.ParameterSets.ToImmutableArray()
            : ImmutableArray<string>.Empty;
        return new ParameterDefinition(
            Name: parameter.Name,
            Type: humanizedType,
            IsMandatory: parameter.IsMandatory,
            IsSecure: isSecure,
            Description: null,
            DefaultValue: null,
            Aliases: aliases,
            ParameterSets: parameterSets);
    }

    private static SchemaDefinition BuildSchema(ImmutableArray<ParameterDefinition> parameters)
    {
        if (parameters.IsDefaultOrEmpty)
        {
            return new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray<SchemaProperty>.Empty,
                Required: ImmutableArray<string>.Empty,
                Items: null);
        }

        var propertyBuilder = ImmutableArray.CreateBuilder<SchemaProperty>(parameters.Length);
        var requiredBuilder = ImmutableArray.CreateBuilder<string>(parameters.Length);
        foreach (var def in parameters)
        {
            propertyBuilder.Add(new SchemaProperty(
                Name: def.Name,
                Type: def.Type,
                Enum: null,
                Minimum: null,
                Maximum: null,
                Pattern: null,
                Schema: null));
            if (def.IsMandatory)
            {
                requiredBuilder.Add(def.Name);
            }
        }

        return new SchemaDefinition(
            Type: "object",
            Properties: propertyBuilder.ToImmutable(),
            Required: requiredBuilder.ToImmutable(),
            Items: null);
    }

    // CommandMetadata carries OutputType as a list of strings (one per declared
    // [OutputType] argument). The IR's OutputMetadata record holds a single
    // name + optional generic-args list; the mapper uses the first declared type
    // as the canonical name and drops the rest, matching the script-module
    // introspector's behavior of recording the first [OutputType(...)].
    private static OutputMetadata? MapOutput(List<string> outputType)
    {
        if (outputType is null || outputType.Count == 0)
        {
            return null;
        }
        return new OutputMetadata(outputType[0], OutputTypeArguments: null);
    }

    // Mirrors ScriptModuleIntrospector.IsSecureType: PowerShell type names are
    // case-insensitive, and the humanizer preserves the author-supplied casing.
    // Both SecureString and PSCredential are recognized as secure types.
    private static bool IsSecureType(string type) =>
        string.Equals(type, "SecureString", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "PSCredential", StringComparison.OrdinalIgnoreCase);
}

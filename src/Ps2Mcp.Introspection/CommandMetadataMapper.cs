using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
        var schema = SchemaBuilder.FromParameters(parameters);
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
            Execution: new ExecutionDefinition(ExecutionDefinition.DefaultSerializationDepth),
            Help: null,
            Output: output);
    }

    private static ParameterDefinition MapParameter(BinaryParameterPayload parameter)
    {
        var humanizedType = TypeNameHumanizer.Humanize(parameter.Type);
        var isSecure = PowerShellTypeMapper.IsSecureType(humanizedType);
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

    // CommandMetadata carries OutputType as a list of strings (one per declared
    // [OutputType] argument). The IR's OutputMetadata record holds a single
    // name + optional generic-args list; the mapper uses the first declared type
    // as the canonical name and drops the rest, matching the script-module
    // introspector's behavior of recording the first [OutputType(...)].
    private static OutputMetadata? MapOutput(List<string> outputTypes)
    {
        if (outputTypes is null || outputTypes.Count == 0)
        {
            return null;
        }
        return new OutputMetadata(outputTypes[0], OutputTypeArguments: null);
    }

}

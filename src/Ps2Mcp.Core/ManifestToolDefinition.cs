using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

/// <summary>
/// Represents a single MCP tool entry in a manifest, mapping a PowerShell command to its schema
/// and parameter metadata.
/// </summary>
/// <param name="ToolName">The tool identifier exposed to MCP clients.</param>
/// <param name="SourceCommand">The PowerShell command that implements this tool.</param>
/// <param name="Parameters">The parameter definitions for this tool.</param>
/// <param name="RequiredParameterSet">The default parameter set name, if specified.</param>
/// <param name="Schema">The JSON Schema for the tool's input parameters.</param>
public sealed record ManifestToolDefinition(
    [property: JsonPropertyOrder(1)] string ToolName,
    [property: JsonPropertyOrder(2)] string SourceCommand,
    [property: JsonPropertyOrder(3)] ImmutableArray<ManifestParameterDefinition> Parameters,
    [property: JsonPropertyOrder(4)] string? RequiredParameterSet,
    [property: JsonPropertyOrder(5)] SchemaDefinition Schema)
{
    internal static ManifestToolDefinition FromTool(ToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        return new ManifestToolDefinition(
            ToolName: tool.ToolName,
            SourceCommand: tool.SourceCommand,
            Parameters: (tool.Parameters.IsDefault ? ImmutableArray<ParameterDefinition>.Empty : tool.Parameters)
                .Select(ManifestParameterDefinition.FromParameter).ToImmutableArray(),
            RequiredParameterSet: tool.RequiredParameterSet,
            Schema: tool.Schema);
    }

    public bool Equals(ManifestToolDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ToolName == other.ToolName
            && SourceCommand == other.SourceCommand
            && SequenceEqualityHelpers.SequenceEqual(Parameters, other.Parameters)
            && RequiredParameterSet == other.RequiredParameterSet
            && Schema == other.Schema;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ToolName);
        hash.Add(SourceCommand);
        SequenceEqualityHelpers.AddToHash(ref hash, Parameters);
        hash.Add(RequiredParameterSet);
        hash.Add(Schema);
        return hash.ToHashCode();
    }
}

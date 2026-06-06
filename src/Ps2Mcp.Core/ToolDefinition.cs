using System;
using System.Collections.Immutable;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

public sealed record ToolDefinition(
    string ToolName,
    string SourceCommand,
    string Description,
    ImmutableArray<ParameterDefinition> Parameters,
    string? RequiredParameterSet,
    SchemaDefinition Schema,
    ExecutionDefinition Execution,
    HelpMetadata? Help,
    OutputMetadata? Output)
{
    public bool Equals(ToolDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ToolName == other.ToolName
            && SourceCommand == other.SourceCommand
            && Description == other.Description
            && SequenceEqualityHelpers.SequenceEqual(Parameters, other.Parameters)
            && RequiredParameterSet == other.RequiredParameterSet
            && Schema == other.Schema
            && Execution == other.Execution
            && Help == other.Help
            && Output == other.Output;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ToolName);
        hash.Add(SourceCommand);
        hash.Add(Description);
        SequenceEqualityHelpers.AddToHash(ref hash, Parameters);
        hash.Add(RequiredParameterSet);
        hash.Add(Schema);
        hash.Add(Execution);
        hash.Add(Help);
        hash.Add(Output);
        return hash.ToHashCode();
    }
}

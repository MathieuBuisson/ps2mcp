using System;
using System.Collections.Immutable;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

public sealed record McpServerDefinition(
    ModuleDefinition Module,
    ImmutableArray<ToolDefinition> Tools,
    int IrVersion = IrVersion.Current)
{
    public bool Equals(McpServerDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return IrVersion == other.IrVersion
            && Module == other.Module
            && SequenceEqualityHelpers.SequenceEqual(Tools, other.Tools);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IrVersion);
        hash.Add(Module);
        SequenceEqualityHelpers.AddToHash(ref hash, Tools);
        return hash.ToHashCode();
    }
}

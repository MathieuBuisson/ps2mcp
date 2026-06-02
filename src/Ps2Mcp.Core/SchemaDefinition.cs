using System;
using System.Collections.Immutable;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

public sealed record SchemaDefinition(
    string Type,
    ImmutableArray<SchemaProperty> Properties,
    ImmutableArray<string> Required,
    SchemaDefinition? Items)
{
    public bool Equals(SchemaDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type
            && SequenceEqualityHelpers.SequenceEqual(Properties, other.Properties)
            && SequenceEqualityHelpers.SequenceEqual(Required, other.Required)
            && Items == other.Items;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        SequenceEqualityHelpers.AddToHash(hash, Properties);
        SequenceEqualityHelpers.AddToHash(hash, Required);
        hash.Add(Items);
        return hash.ToHashCode();
    }
}

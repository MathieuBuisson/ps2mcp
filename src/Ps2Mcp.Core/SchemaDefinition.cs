using System;
using System.Collections.Immutable;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

public sealed record SchemaDefinition(
    string Type,
    ImmutableArray<SchemaProperty> Properties,
    ImmutableArray<string> Required,
    SchemaDefinition? Items,
    string? ComplexType = null)
{
    public bool Equals(SchemaDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type
            && SequenceEqualityHelpers.SequenceEqual(Properties, other.Properties)
            && SequenceEqualityHelpers.SequenceEqual(Required, other.Required)
            && Items == other.Items
            && ComplexType == other.ComplexType;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        SequenceEqualityHelpers.AddToHash(ref hash, Properties);
        SequenceEqualityHelpers.AddToHash(ref hash, Required);
        hash.Add(Items);
        hash.Add(ComplexType);
        return hash.ToHashCode();
    }
}

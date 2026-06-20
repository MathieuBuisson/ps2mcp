using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

public sealed record SchemaDefinition(
    [property: JsonPropertyOrder(1)] string Type,
    [property: JsonPropertyOrder(2)] ImmutableArray<SchemaProperty> Properties,
    [property: JsonPropertyOrder(3)] ImmutableArray<string> Required,
    [property: JsonPropertyOrder(4)] SchemaDefinition? Items,
    [property: JsonPropertyOrder(5)] string? ComplexType = null)
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

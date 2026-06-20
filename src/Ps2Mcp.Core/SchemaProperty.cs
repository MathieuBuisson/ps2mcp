using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

// Minimum/Maximum kept as strings to support both integer and number bounds without a discriminator.
// Schema carries the sub-shape when a property is itself an object or an array (e.g., nested object or array of items).
public sealed record SchemaProperty(
    [property: JsonPropertyOrder(1)] string Name,
    [property: JsonPropertyOrder(2)] string Type,
    [property: JsonPropertyOrder(3)] ImmutableArray<string>? Enum,
    [property: JsonPropertyOrder(4)] string? Minimum,
    [property: JsonPropertyOrder(5)] string? Maximum,
    [property: JsonPropertyOrder(6)] string? Pattern,
    [property: JsonPropertyOrder(7)] SchemaDefinition? Schema)
{
    public bool Equals(SchemaProperty? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && Type == other.Type
            && SequenceEqualityHelpers.NullableSequenceEqual(Enum, other.Enum)
            && Minimum == other.Minimum
            && Maximum == other.Maximum
            && Pattern == other.Pattern
            && Schema == other.Schema;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name);
        hash.Add(Type);
        SequenceEqualityHelpers.AddNullableToHash(ref hash, Enum);
        hash.Add(Minimum);
        hash.Add(Maximum);
        hash.Add(Pattern);
        hash.Add(Schema);
        return hash.ToHashCode();
    }
}

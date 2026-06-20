using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

public sealed record OutputMetadata(
    [property: JsonPropertyOrder(1)] string? OutputTypeName,
    [property: JsonPropertyOrder(2)] ImmutableArray<string>? OutputTypeArguments)
{
    public bool Equals(OutputMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return OutputTypeName == other.OutputTypeName
            && SequenceEqualityHelpers.NullableSequenceEqual(OutputTypeArguments, other.OutputTypeArguments);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(OutputTypeName);
        SequenceEqualityHelpers.AddNullableToHash(ref hash, OutputTypeArguments);
        return hash.ToHashCode();
    }
}

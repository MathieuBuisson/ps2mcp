using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

public sealed record HelpMetadata(
    [property: JsonPropertyOrder(1)] string? Synopsis,
    [property: JsonPropertyOrder(2)] string? Description,
    [property: JsonPropertyOrder(3)] ImmutableArray<HelpExample> Examples)
{
    public bool Equals(HelpMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Synopsis == other.Synopsis
            && Description == other.Description
            && SequenceEqualityHelpers.SequenceEqual(Examples, other.Examples);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Synopsis);
        hash.Add(Description);
        SequenceEqualityHelpers.AddToHash(ref hash, Examples);
        return hash.ToHashCode();
    }
}

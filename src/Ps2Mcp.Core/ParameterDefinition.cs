using System;
using System.Collections.Immutable;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

public sealed record ParameterDefinition(
    string Name,
    string Type,
    bool IsMandatory,
    bool IsSecure,
    string? Description,
    string? DefaultValue,
    ImmutableArray<string> Aliases,
    ImmutableArray<string> ParameterSets)
{
    public bool Equals(ParameterDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && Type == other.Type
            && IsMandatory == other.IsMandatory
            && IsSecure == other.IsSecure
            && Description == other.Description
            && DefaultValue == other.DefaultValue
            && SequenceEqualityHelpers.SequenceEqual(Aliases, other.Aliases)
            && SequenceEqualityHelpers.SequenceEqual(ParameterSets, other.ParameterSets);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name);
        hash.Add(Type);
        hash.Add(IsMandatory);
        hash.Add(IsSecure);
        hash.Add(Description);
        hash.Add(DefaultValue);
        SequenceEqualityHelpers.AddToHash(hash, Aliases);
        SequenceEqualityHelpers.AddToHash(hash, ParameterSets);
        return hash.ToHashCode();
    }
}

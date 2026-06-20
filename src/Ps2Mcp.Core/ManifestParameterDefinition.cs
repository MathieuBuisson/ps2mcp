using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

/// <summary>
/// Represents a single parameter of an MCP tool entry in a manifest.
/// </summary>
/// <param name="Name">The parameter name.</param>
/// <param name="Type">The mapped JSON Schema type string (e.g. "string", "integer").</param>
/// <param name="IsMandatory">Whether the parameter is required.</param>
/// <param name="IsSecure">Whether the parameter holds sensitive data (e.g. credentials).</param>
/// <param name="Aliases">Alternative names accepted by the parameter.</param>
/// <param name="ParameterSets">The parameter set names this parameter belongs to.</param>
public sealed record ManifestParameterDefinition(
    [property: JsonPropertyOrder(1)] string Name,
    [property: JsonPropertyOrder(2)] string Type,
    [property: JsonPropertyOrder(3)] bool IsMandatory,
    [property: JsonPropertyOrder(4)] bool IsSecure,
    [property: JsonPropertyOrder(5)] ImmutableArray<string> Aliases,
    [property: JsonPropertyOrder(6)] ImmutableArray<string> ParameterSets)
{
    internal static ManifestParameterDefinition FromParameter(ParameterDefinition parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        return new ManifestParameterDefinition(
            Name: parameter.Name,
            Type: parameter.Type,
            IsMandatory: parameter.IsMandatory,
            IsSecure: parameter.IsSecure,
            Aliases: parameter.Aliases,
            ParameterSets: parameter.ParameterSets);
    }

    public bool Equals(ManifestParameterDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && Type == other.Type
            && IsMandatory == other.IsMandatory
            && IsSecure == other.IsSecure
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
        SequenceEqualityHelpers.AddToHash(ref hash, Aliases);
        SequenceEqualityHelpers.AddToHash(ref hash, ParameterSets);
        return hash.ToHashCode();
    }
}

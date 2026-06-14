using System;
using System.Collections.Immutable;

namespace Ps2Mcp.Introspection;

/// <summary>
/// Maps PowerShell validation attributes (<see cref="ParameterAttributeInfo"/>)
/// into schema-level constraints (enum, bounds, pattern) for the IR.
/// </summary>
/// <remarks>
/// The mapper is a pure function: it reads the extracted validation metadata
/// from <see cref="ParameterAttributeInfo"/> and produces a
/// <see cref="ValidationMapping"/> that the schema builder consumes. It does
/// not perform any validation itself — inverted ranges, empty sets, and
/// syntactically invalid regex patterns are passed through verbatim so the
/// schema emitter can decide how to surface them.
/// </remarks>
internal static class ValidationMapper
{
    public static ValidationMapping Map(ParameterAttributeInfo attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        return new ValidationMapping(
            Enum: attributes.HasValidateSet ? attributes.ValidateSetValues : null,
            Minimum: attributes.ValidateRangeMin,
            Maximum: attributes.ValidateRangeMax,
            Pattern: attributes.HasValidatePattern ? attributes.ValidatePattern : null);
    }
}

internal readonly record struct ValidationMapping(
    ImmutableArray<string>? Enum,
    string? Minimum,
    string? Maximum,
    string? Pattern)
{
    /// <summary>
    /// Returns <c>true</c> if any validation constraint is present.
    /// An empty <see cref="Enum"/> array (e.g., <c>[ValidateSet()]</c>) is considered a constraint.
    /// </summary>
    public bool HasConstraints =>
        Enum.HasValue || Minimum is not null || Maximum is not null || Pattern is not null;
}

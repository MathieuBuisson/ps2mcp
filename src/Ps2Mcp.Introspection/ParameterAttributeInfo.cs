using System;
using System.Collections.Immutable;

namespace Ps2Mcp.Introspection;

/// <summary>
/// Captures every attribute-like syntactic annotation on a
/// <see cref="System.Management.Automation.Language.ParameterAst"/>, including the type constraint
/// (<see cref="System.Management.Automation.Language.TypeConstraintAst"/>) and all
/// <see cref="System.Management.Automation.Language.AttributeAst"/> instances
/// (<c>[Parameter()]</c>, <c>[ValidateSet()]</c>, <c>[Alias()]</c>, and so on).
/// </summary>
/// <remarks>
/// Both <c>TypeConstraintAst</c> and <c>AttributeAst</c> derive from
/// <c>AttributeBaseAst</c> and live in the same <c>ParameterAst.Attributes</c> collection, so this
/// record represents the union of what can be extracted from that collection in a single pass.
/// <para>
/// <see cref="Type"/> is the humanized form of the type constraint as written by the module author.
/// The extraction pipeline applies four transformations: strip the CLR generic-arity marker
/// (<c>`N</c>), collapse the nested-generic markers (<c>[[</c> / <c>]]</c>) to single brackets,
/// strip CLR-looking namespace prefixes (<c>System.*</c> and <c>Microsoft.*</c>), and leave the
/// result un-bracketed. Application namespaces (anything not starting with <c>System.</c> or
/// <c>Microsoft.</c>) are preserved verbatim.
/// </para>
/// <para>
/// Examples:
/// <list type="bullet">
///   <item><description><c>[string]</c> → <c>"string"</c></description></item>
///   <item><description><c>[int]</c> → <c>"int"</c></description></item>
///   <item><description><c>[SecureString]</c> → <c>"SecureString"</c></description></item>
///   <item><description><c>[List[string]]</c> → <c>"List[string]"</c></description></item>
///   <item><description><c>[System.Collections.Generic.List[string]]</c> → <c>"List[string]"</c></description></item>
///   <item><description><c>[Nullable[Nullable[int]]]</c> → <c>"Nullable[Nullable[int]]"</c></description></item>
///   <item><description><c>[MyApp.Services.Foo[System.String]]</c> → <c>"MyApp.Foo[string]"</c></description></item>
/// </list>
/// </para>
/// <para>
/// When multiple type constraints are declared (for example <c>[int][string]</c>), the rightmost
/// one wins, matching PowerShell's own type-resolution semantics. When no type constraint is
/// declared, the value is <c>"object"</c> per §15's conservative fallback rule.
/// </para>
/// <para>
/// <see cref="IsMandatory"/> is <c>true</c> if any of the <c>[Parameter()]</c> declarations on the
/// parameter marks it as mandatory. The IR does not carry per-parameter-set mandatory flags in v1.
/// </para>
/// <para>
/// <see cref="ParameterSets"/> is the union of <c>ParameterSetName</c> values across all
/// <c>[Parameter()]</c> declarations. An empty array means the parameter belongs only to the
/// unnamed default parameter set.
/// </para>
/// <para>
/// <see cref="ValidateRangeMin"/> and <see cref="ValidateRangeMax"/> are the invariant-culture
/// string form of the bound's <c>ToString</c> output, captured verbatim from the AST constant
/// expression. Any of the .NET numeric primitive types (SByte, Byte, Int16, Int32, Int64, UInt16,
/// UInt32, UInt64, Single, Double, Decimal) are accepted, so values such as <c>0.5</c>,
/// <c>2147483648</c> (larger than <see cref="int.MaxValue"/>), and <c>-0.1</c> survive extraction
/// without precision loss. Non-numeric arguments (string constants, booleans, variables) leave the
/// corresponding bound as <c>null</c> per §13.3 — the schema emitter surfaces the omission. The
/// string form is the round-trip-safe representation chosen so the schema emitter (Phase 8) can
/// re-parse the bound in the type context it needs without the extractor fabricating precision.
/// </para>
/// </remarks>
public sealed record ParameterAttributeInfo(
    string Type,
    bool IsMandatory,
    ImmutableArray<string> ParameterSets,
    ImmutableArray<string> Aliases,
    ImmutableArray<string>? ValidateSetValues,
    string? ValidateRangeMin,
    string? ValidateRangeMax,
    string? ValidatePattern,
    bool AllowNull,
    bool AllowEmptyString,
    bool AllowEmptyCollection)
{
    /// <summary>
    /// Gets a value indicating whether a <c>[ValidateSet()]</c> attribute was present on the
    /// parameter, regardless of whether any values were supplied.
    /// </summary>
    /// <remarks>
    /// A <c>[ValidateSet()]</c> attribute declared with no arguments is syntactically valid in
    /// PowerShell and is preserved here as a non-null empty array. The value is <c>null</c> only
    /// when the attribute is entirely absent from the parameter.
    /// </remarks>
    public bool HasValidateSet => ValidateSetValues.HasValue;

    /// <summary>
    /// Gets a value indicating whether a <c>[ValidateRange()]</c> attribute supplied at least one
    /// bound on the parameter.
    /// </summary>
    public bool HasValidateRange => ValidateRangeMin is not null || ValidateRangeMax is not null;

    /// <summary>
    /// Gets a value indicating whether a <c>[ValidatePattern()]</c> attribute was present on the
    /// parameter.
    /// </summary>
    public bool HasValidatePattern => ValidatePattern is not null;
}

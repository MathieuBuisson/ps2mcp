using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Management.Automation.Language;

namespace Ps2Mcp.Introspection;

/// <summary>
/// Extracts every attribute-like syntactic annotation from a
/// <see cref="ParameterAst"/> into a <see cref="ParameterAttributeInfo"/>.
/// </summary>
/// <remarks>
/// Covers the attribute families enumerated in §13.1 of the specification: <c>Parameter</c>,
/// <c>ValidateSet</c>, <c>ValidateRange</c>, <c>ValidatePattern</c>, <c>Alias</c>,
/// <c>AllowNull</c>, <c>AllowEmptyString</c>, and <c>AllowEmptyCollection</c>. Attribute names are
/// matched case-insensitively on the rightmost segment of <c>AttributeAst.TypeName.Name</c> with
/// an optional <c>Attribute</c> suffix stripped, mirroring PowerShell's own attribute resolution
/// semantics. This means <c>[Alias]</c>, <c>[AliasAttribute]</c>, and
/// <c>[System.Management.Automation.AliasAttribute]</c> all resolve to the same canonical short
/// name. Attribute types not enumerated above are silently ignored so future PowerShell-side
/// additions do not break extraction.
/// <para>
/// <c>ValidateRange</c> bounds are captured as the invariant-culture string form of the
/// constant expression's value, taken from any of the .NET numeric primitive types (SByte, Byte,
/// Int16, Int32, Int64, UInt16, UInt32, UInt64, Single, Double, Decimal). This preserves values
/// such as <c>0.5</c> and <c>2147483648</c> (beyond <see cref="int.MaxValue"/>) without
/// precision loss or fabricated type coercion. Non-numeric arguments (string constants,
/// booleans, variables, and other non-numeric expressions) are silently skipped per §13.3 to
/// avoid fabricating precision; the schema emitter is expected to surface the omission. The
/// string form is round-trip-safe so the schema emitter (Phase 8) can re-parse each bound in
/// the type context its target needs.
/// </para>
/// </remarks>
public static partial class ParameterAttributeExtractor
{
    /// <summary>
    /// Extracts a <see cref="ParameterAttributeInfo"/> from the given <see cref="ParameterAst"/>.
    /// </summary>
    /// <param name="parameter">The parameter whose annotations are to be extracted.</param>
    /// <returns>A populated <see cref="ParameterAttributeInfo"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is <c>null</c>.</exception>
    public static ParameterAttributeInfo Extract(ParameterAst parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        // All per-family state is declared here and updated in a single walk of
        // parameter.Attributes. The collection is small (typically under 10 items) but the
        // co-location makes every field's derivation visible in one place.
        string? type = null;
        var isMandatory = false;
        var parameterSets = ImmutableArray.CreateBuilder<string>();
        var seenParameterSets = new HashSet<string>(StringComparer.Ordinal);
        var aliases = ImmutableArray.CreateBuilder<string>();
        var validateSetBuilder = ImmutableArray.CreateBuilder<string>();
        var validateSetFound = false;
        string? validateRangeMin = null;
        string? validateRangeMax = null;
        string? validatePattern = null;
        var allowNull = false;
        var allowEmptyString = false;
        var allowEmptyCollection = false;

        foreach (var attr in parameter.Attributes)
        {
            switch (attr)
            {
                case TypeConstraintAst typeConstraint:
                    // PowerShell treats multiple type constraints as a right-to-left conversion
                    // chain: the rightmost constraint wins, and earlier ones are "convertible to"
                    // hints. Verified empirically:
                    //   function f { param([int][string]$x) $x.GetType().Name }
                    // returns "String", not "Int32".
                    type = HumanizeTypeName(typeConstraint.TypeName);
                    break;
                case AttributeAst attribute when IsAttributeNamed(attribute, "Parameter"):
                    if (IsMandatorySet(attribute))
                    {
                        isMandatory = true;
                    }
                    var setName = ExtractParameterSetName(attribute);
                    if (setName is not null && seenParameterSets.Add(setName))
                    {
                        parameterSets.Add(setName);
                    }
                    break;
                case AttributeAst attribute when IsAttributeNamed(attribute, "Alias"):
                    foreach (var arg in attribute.PositionalArguments)
                    {
                        if (arg is StringConstantExpressionAst s)
                        {
                            aliases.Add(s.Value);
                        }
                    }
                    break;
                case AttributeAst attribute when IsAttributeNamed(attribute, "ValidateSet"):
                    // [ValidateSet()] declared with no arguments is syntactically valid PowerShell.
                    // The tri-state distinction (null = absent, empty = present with no values,
                    // non-empty = values) lets the orchestrator distinguish "no enum constraint"
                    // from "closed enum with zero options" per §13.3.
                    validateSetFound = true;
                    foreach (var arg in attribute.PositionalArguments)
                    {
                        if (arg is StringConstantExpressionAst s)
                        {
                            validateSetBuilder.Add(s.Value);
                        }
                    }
                    break;
                case AttributeAst attribute when IsAttributeNamed(attribute, "ValidateRange"):
                    var pos = attribute.PositionalArguments;
                    if (pos.Count >= 1 && pos[0] is ConstantExpressionAst { Value: not null } c0 &&
                        TryFormatNumeric(c0.Value, out var minText))
                    {
                        validateRangeMin = minText;
                    }
                    if (pos.Count >= 2 && pos[1] is ConstantExpressionAst { Value: not null } c1 &&
                        TryFormatNumeric(c1.Value, out var maxText))
                    {
                        validateRangeMax = maxText;
                    }
                    break;
                case AttributeAst attribute when IsAttributeNamed(attribute, "ValidatePattern"):
                    if (attribute.PositionalArguments.Count >= 1 &&
                        attribute.PositionalArguments[0] is StringConstantExpressionAst s2)
                    {
                        validatePattern = s2.Value;
                    }
                    break;
                case AttributeAst attribute when IsAttributeNamed(attribute, "AllowNull"):
                    allowNull = true;
                    break;
                case AttributeAst attribute when IsAttributeNamed(attribute, "AllowEmptyString"):
                    allowEmptyString = true;
                    break;
                case AttributeAst attribute when IsAttributeNamed(attribute, "AllowEmptyCollection"):
                    allowEmptyCollection = true;
                    break;
            }
        }

        return new ParameterAttributeInfo(
            type ?? "object",
            isMandatory,
            parameterSets.ToImmutable(),
            aliases.ToImmutable(),
            validateSetFound ? validateSetBuilder.ToImmutable() : null,
            validateRangeMin,
            validateRangeMax,
            validatePattern,
            allowNull,
            allowEmptyString,
            allowEmptyCollection);
    }

    // Humanization is delegated to the shared TypeNameHumanizer; the pipeline (strip
    // generic-arity, collapse nested brackets, strip CLR namespace prefix) is identical
    // for ITypeName and the raw FullName string used by the binary mapper.
    private static string HumanizeTypeName(ITypeName typeName)
    {
        var raw = typeName.FullName;
        if (string.IsNullOrEmpty(raw))
        {
            raw = typeName.Name;
        }
        return TypeNameHumanizer.Humanize(raw);
    }

    // Tries to format a constant value as an invariant-culture string when the value is one of
    // the .NET numeric primitive types (SByte through Decimal). Used by ValidateRange bound
    // extraction to preserve the user's value without fabricating precision and without losing
    // range. Non-numeric constants (strings, booleans, characters) are rejected so the
    // corresponding bound stays null per §13.3's "don't fabricate" rule.
    private static bool TryFormatNumeric(object value, out string? formatted)
    {
        var typeCode = Type.GetTypeCode(value.GetType());
        if (typeCode >= TypeCode.SByte && typeCode <= TypeCode.Decimal)
        {
            formatted = ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
            return true;
        }
        formatted = null;
        return false;
    }

    // Normalizes an attribute type name to its canonical short form: takes the rightmost
    // namespace segment and strips an optional "Attribute" suffix. This collapses [Alias],
    // [AliasAttribute], and [System.Management.Automation.AliasAttribute] to the same short
    // name "Alias". The suffix strip is case-insensitive to match PowerShell's attribute
    // resolution (so [ALIASATTRIBUTE] and [aliasattribute] also match).
    private static string NormalizeAttributeName(string typeName)
    {
        var lastDot = typeName.LastIndexOf('.');
        var shortName = lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
        return shortName.Length > "Attribute".Length &&
            shortName.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase)
            ? shortName[..^"Attribute".Length]
            : shortName;
    }

    /// <summary>
    /// Returns <c>true</c> when the given attribute's type name resolves to the supplied
    /// short name, case-insensitively and with an optional <c>Attribute</c> suffix ignored.
    /// </summary>
    /// <remarks>
    /// Exposed (not just internal) because the attribute-matching step is needed outside this
    /// extractor too — for example, <c>ScriptModuleIntrospector</c> matches
    /// <c>[OutputType()]</c> by short name. Duplicating the rightmost-segment + suffix-strip
    /// logic in every caller would be a maintenance hazard.
    /// </remarks>
    public static bool IsAttributeNamed(AttributeAst attribute, string name) =>
        string.Equals(NormalizeAttributeName(attribute.TypeName.Name), name, StringComparison.OrdinalIgnoreCase);

    private static bool IsMandatorySet(AttributeAst parameterAttribute)
    {
        foreach (var named in parameterAttribute.NamedArguments)
        {
            if (string.Equals(named.ArgumentName, "Mandatory", StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateBooleanArgument(named);
            }
        }
        return false;
    }

    // Returns null when the [Parameter()] attribute does not declare a ParameterSetName; the caller
    // omits it, resulting in an empty ParameterSets array for the default parameter set.
    private static string? ExtractParameterSetName(AttributeAst parameterAttribute)
    {
        foreach (var named in parameterAttribute.NamedArguments)
        {
            if (!string.Equals(named.ArgumentName, "ParameterSetName", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (named.Argument is StringConstantExpressionAst s)
            {
                return s.Value;
            }
        }
        return null;
    }

    private static bool EvaluateBooleanArgument(NamedAttributeArgumentAst arg)
    {
        // PowerShell synthesizes the Argument expression for shorthand named arguments, so
        // [Parameter(Mandatory)] arrives here as a VariableExpressionAst with UserPath "true".
        if (arg.Argument is VariableExpressionAst variable &&
            string.Equals(variable.VariablePath.UserPath, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (arg.Argument is ConstantExpressionAst constant && constant.Value is bool boolean)
        {
            return boolean;
        }
        return false;
    }
}

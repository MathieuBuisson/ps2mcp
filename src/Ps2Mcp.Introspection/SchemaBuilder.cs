using System;
using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Introspection;

/// <summary>
/// Builds a <see cref="SchemaDefinition"/> from a list of <see cref="ParameterDefinition"/>s
/// by mapping each parameter's PowerShell type and optional validation constraints into
/// JSON-Schema-typed <see cref="SchemaProperty"/> objects.
/// </summary>
/// <remarks>
/// This class consolidates the schema-building logic shared between
/// <see cref="CommandMetadataMapper"/> (binary modules, no validation attributes) and
/// <see cref="ScriptModuleIntrospector"/> (script modules, full validation attributes).
/// </remarks>
internal static class SchemaBuilder
{
    public static SchemaDefinition FromParameters(
        ImmutableArray<ParameterDefinition> parameters,
        Func<ParameterDefinition, ValidationMapping>? validationSelector = null)
    {
        if (parameters.IsDefaultOrEmpty)
        {
            return new SchemaDefinition(
                Type: "object",
                Properties: ImmutableArray<SchemaProperty>.Empty,
                Required: ImmutableArray<string>.Empty,
                Items: null);
        }

        var propertyBuilder = ImmutableArray.CreateBuilder<SchemaProperty>(parameters.Length);
        var requiredBuilder = ImmutableArray.CreateBuilder<string>(parameters.Length);
        foreach (var def in parameters)
        {
            var mappedType = PowerShellTypeMapper.Map(def.Type);
            var validation = validationSelector?.Invoke(def) ?? default;
            var propertySchema = mappedType.Schema ?? (mappedType.ComplexType is not null
                ? mappedType.ToSchemaDefinition()
                : null);
            propertyBuilder.Add(new SchemaProperty(
                Name: def.Name,
                Type: mappedType.Type,
                Enum: validation.Enum,
                Minimum: validation.Minimum,
                Maximum: validation.Maximum,
                Pattern: validation.Pattern,
                Schema: propertySchema));
            if (def.IsMandatory)
            {
                requiredBuilder.Add(def.Name);
            }
        }

        return new SchemaDefinition(
            Type: "object",
            Properties: propertyBuilder.ToImmutable(),
            Required: requiredBuilder.ToImmutable(),
            Items: null);
    }
}

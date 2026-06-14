using System;
using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Introspection;

internal static class PowerShellTypeMapper
{
    public static PowerShellTypeMapping Map(string powerShellType)
    {
        var normalizedType = NormalizeTypeName(powerShellType);
        if (normalizedType.EndsWith("[]", StringComparison.Ordinal))
        {
            var itemType = normalizedType[..^2];
            var itemMapping = Map(itemType);

            return new PowerShellTypeMapping(
                Type: "array",
                ComplexType: null,
                Schema: new SchemaDefinition(
                    Type: "array",
                    Properties: ImmutableArray<SchemaProperty>.Empty,
                    Required: ImmutableArray<string>.Empty,
                    Items: itemMapping.ToSchemaDefinition()));
        }

        var mappedType = MapScalarType(normalizedType);
        var isComplex = string.Equals(mappedType, "object", StringComparison.Ordinal)
            && !string.Equals(normalizedType, "object", StringComparison.OrdinalIgnoreCase);

        return new PowerShellTypeMapping(
            Type: mappedType,
            ComplexType: isComplex ? normalizedType : null,
            Schema: null);
    }

    public static bool IsSecureType(string powerShellType)
    {
        var normalizedType = NormalizeTypeName(powerShellType);

        return string.Equals(normalizedType, "SecureString", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedType, "PSCredential", StringComparison.OrdinalIgnoreCase);
    }

    private static string MapScalarType(string powerShellType) => powerShellType.ToLowerInvariant() switch
    {
        "string" => "string",
        "byte" or "sbyte" or
        "short" or "int16" or "ushort" or "uint16" or
        "int" or "int32" or "uint" or "uint32" or
        "long" or "int64" or "ulong" or "uint64" => "integer",
        "double" or "decimal" or "float" or "single" => "number",
        "bool" or "boolean" or "switch" or "switchparameter" => "boolean",
        "object" => "object",
        _ => "object",
    };

    private static string NormalizeTypeName(string powerShellType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(powerShellType);

        return TypeNameHumanizer.Humanize(powerShellType.Trim());
    }
}

internal readonly record struct PowerShellTypeMapping(
    string Type,
    string? ComplexType,
    SchemaDefinition? Schema)
{
    public SchemaDefinition ToSchemaDefinition() => Schema ?? new SchemaDefinition(
        Type,
        Properties: ImmutableArray<SchemaProperty>.Empty,
        Required: ImmutableArray<string>.Empty,
        Items: null,
        ComplexType: ComplexType);
}

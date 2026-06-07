using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ps2Mcp.Introspection;

// JSON deserialization shape for the output of Introspection.ps1. These are the
// wire-format types only — the mapper converts them into IR records. Properties
// are public read/write for the source-generated JSON serializer; field visibility
// matches the JSON key names exactly so no [JsonPropertyName] is required.
// List<T> is used for collections because the source-generated serializer
// populates mutable collections directly; ImmutableArray<T> would require a
// custom converter. CommandMetadataMapper.Map converts each List<T> into the
// ImmutableArray<T> used by the IR records.
public sealed class BinaryIntrospectionPayload
{
    public string ModuleName { get; set; } = string.Empty;
    public string ModulePath { get; set; } = string.Empty;
    public List<BinaryCommandPayload> Commands { get; set; } = new();
}

public sealed class BinaryCommandPayload
{
    public string Name { get; set; } = string.Empty;
    public string CommandType { get; set; } = string.Empty;
    public bool SupportsShouldProcess { get; set; }
    public bool SupportsPaging { get; set; }
    public bool SupportsTransactions { get; set; }
    public string DefaultParameterSetName { get; set; } = string.Empty;
    public string? HelpUri { get; set; }
    public List<string> OutputType { get; set; } = new();
    public List<string> Aliases { get; set; } = new();
    public List<BinaryParameterPayload> Parameters { get; set; } = new();
    public List<BinaryParameterSetPayload> ParameterSets { get; set; } = new();
}

public sealed class BinaryParameterPayload
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public int Position { get; set; }
    public bool ValueFromPipeline { get; set; }
    public bool ValueFromPipelineByPropertyName { get; set; }
    public bool ValueFromRemainingArguments { get; set; }
    public List<string> Aliases { get; set; } = new();
    public bool IsSwitch { get; set; }
    public List<string> ParameterSets { get; set; } = new();
}

public sealed class BinaryParameterSetPayload
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

// Source-generated JSON serializer context for the binary-introspection payload.
// AOT-safe (no reflection); rooted on BinaryIntrospectionPayload, which transitively
// pulls in every nested payload type. Public so tests in a separate assembly can
// use the same type info for deserialization round-trips.
// PropertyNameCaseInsensitive=true so the camelCase keys emitted by PowerShell's
// ConvertTo-Json (moduleName, commandType, ...) match the PascalCase C# properties.
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BinaryIntrospectionPayload))]
public sealed partial class BinaryIntrospectionJsonSerializerContext : JsonSerializerContext
{
}

/// <summary>
/// Provides source-generated JSON deserialization for <see cref="BinaryIntrospectionPayload"/>.
/// </summary>
public static class BinaryIntrospectionPayloadSerializer
{
    public static BinaryIntrospectionPayload Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        return JsonSerializer.Deserialize(utf8Json, BinaryIntrospectionJsonSerializerContext.Default.BinaryIntrospectionPayload)!;
    }
}

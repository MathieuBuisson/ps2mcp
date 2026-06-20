using System.Text.Json.Serialization;

namespace Ps2Mcp.Core;

public sealed record ModuleDefinition(
    [property: JsonPropertyOrder(1)] string Name,
    [property: JsonPropertyOrder(2)] string? Version);

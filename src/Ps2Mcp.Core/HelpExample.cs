using System.Text.Json.Serialization;

namespace Ps2Mcp.Core;

public sealed record HelpExample(
    [property: JsonPropertyOrder(1)] string? Title,
    [property: JsonPropertyOrder(2)] string? Code,
    [property: JsonPropertyOrder(3)] string? Remarks);

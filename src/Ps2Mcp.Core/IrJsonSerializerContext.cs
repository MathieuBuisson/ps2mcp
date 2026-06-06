using System.Text.Json.Serialization;

namespace Ps2Mcp.Core;

// The single root type transitively pulls in every IR record reachable from McpServerDefinition.
// Property names are emitted as-declared (PascalCase, matching the record primary-constructor parameter order),
// so identical inputs always serialize to byte-identical output.
[JsonSourceGenerationOptions(
    WriteIndented = true,
    NewLine = "\n")]
[JsonSerializable(typeof(McpServerDefinition))]
internal sealed partial class IrJsonSerializerContext : JsonSerializerContext
{
}

using System.Text.Json.Serialization;

namespace Ps2Mcp.Core;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    NewLine = "\n")]
[JsonSerializable(typeof(ManifestDefinition))]
internal sealed partial class ManifestJsonSerializerContext : JsonSerializerContext
{
}

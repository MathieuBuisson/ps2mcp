using System;
using System.IO;
using System.Text.Json;

namespace Ps2Mcp.Core;

public static class IrJsonSerializer
{
    public static byte[] Serialize(McpServerDefinition server) =>
        JsonSerializer.SerializeToUtf8Bytes(server, IrJsonSerializerContext.Default.McpServerDefinition);

    public static void Serialize(McpServerDefinition server, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        JsonSerializer.Serialize(destination, server, IrJsonSerializerContext.Default.McpServerDefinition);
    }
}

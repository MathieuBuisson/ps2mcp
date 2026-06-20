using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ps2Mcp.Core;

/// <summary>
/// Serializes and deserializes <see cref="ManifestDefinition"/> instances to and from JSON
/// using a source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
/// for AOT compatibility.
/// </summary>
public static class ManifestJsonSerializer
{
    /// <summary>
    /// Serializes the manifest to a UTF-8 byte array.
    /// </summary>
    /// <param name="manifest">The manifest to serialize.</param>
    /// <returns>A UTF-8 encoded JSON byte array.</returns>
    public static byte[] Serialize(ManifestDefinition manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJsonSerializerContext.Default.ManifestDefinition);
    }

    /// <summary>
    /// Deserializes a manifest from a UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON bytes to deserialize.</param>
    /// <returns>The deserialized manifest.</returns>
    public static ManifestDefinition Deserialize(ReadOnlySpan<byte> utf8Json) =>
        JsonSerializer.Deserialize(utf8Json, ManifestJsonSerializerContext.Default.ManifestDefinition)!;

    /// <summary>
    /// Deserializes a manifest from a stream.
    /// </summary>
    /// <param name="utf8Json">The stream containing UTF-8 JSON.</param>
    /// <returns>The deserialized manifest.</returns>
    public static ManifestDefinition Deserialize(Stream utf8Json)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        return JsonSerializer.Deserialize(utf8Json, ManifestJsonSerializerContext.Default.ManifestDefinition)!;
    }

    /// <summary>
    /// Serializes the manifest to a stream synchronously.
    /// </summary>
    /// <param name="manifest">The manifest to serialize.</param>
    /// <param name="destination">The destination stream.</param>
    public static void Serialize(ManifestDefinition manifest, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(destination);
        JsonSerializer.Serialize(destination, manifest, ManifestJsonSerializerContext.Default.ManifestDefinition);
    }

    /// <summary>
    /// Asynchronously serializes the manifest to a stream.
    /// </summary>
    /// <param name="manifest">The manifest to serialize.</param>
    /// <param name="destination">The destination stream.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task SerializeAsync(ManifestDefinition manifest, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(destination);
        return JsonSerializer.SerializeAsync(destination, manifest, ManifestJsonSerializerContext.Default.ManifestDefinition, cancellationToken);
    }
}

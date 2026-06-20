using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ps2Mcp.Core;

/// <summary>
/// Writes a <see cref="ManifestDefinition"/> to disk as indented UTF-8 JSON.
/// </summary>
public static class ManifestWriter
{
    /// <summary>
    /// The fixed filename used for manifest output.
    /// </summary>
    public static readonly string FileName = "manifest.json";

    /// <summary>
    /// Serializes a manifest and writes it to <c>manifest.json</c> inside the specified directory.
    /// </summary>
    /// <param name="manifest">The manifest to serialize.</param>
    /// <param name="outputDirectory">The directory where <c>manifest.json</c> will be written.</param>
    /// <returns>The full path of the written manifest file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outputDirectory"/> is null or whitespace.</exception>
    /// <exception cref="IOException">The directory or file could not be created or written.</exception>
    public static string Write(ManifestDefinition manifest, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullOutputDirectory);

        var manifestPath = Path.Combine(fullOutputDirectory, FileName);
        var bytes = ManifestJsonSerializer.Serialize(manifest);
        File.WriteAllBytes(manifestPath, bytes);
        return manifestPath;
    }

    /// <summary>
    /// Asynchronously serializes a manifest and writes it to <c>manifest.json</c> inside the specified directory.
    /// </summary>
    /// <param name="manifest">The manifest to serialize.</param>
    /// <param name="outputDirectory">The directory where <c>manifest.json</c> will be written.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The full path of the written manifest file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outputDirectory"/> is null or whitespace.</exception>
    /// <exception cref="IOException">The directory or file could not be created or written.</exception>
    public static async Task<string> WriteAsync(ManifestDefinition manifest, string outputDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullOutputDirectory);

        var manifestPath = Path.Combine(fullOutputDirectory, FileName);
        await using var stream = new FileStream(manifestPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await ManifestJsonSerializer.SerializeAsync(manifest, stream, cancellationToken);
        return manifestPath;
    }
}

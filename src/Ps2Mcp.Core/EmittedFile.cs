using System;
using System.IO;

namespace Ps2Mcp.Core;

/// <summary>
/// Represents a file to be written to disk by a server emitter.
/// </summary>
/// <param name="RelativePath">
/// The relative path within the output directory. May use <c>/</c> or <c>\</c> separators;
/// both are accepted by <see cref="Validate"/>. Must not be rooted or contain traversal sequences.
/// </param>
/// <param name="Contents">The file contents to write.</param>
public sealed record EmittedFile(string RelativePath, string Contents)
{
    /// <summary>
    /// Validates that <see cref="RelativePath"/> is a non-empty, relative path without traversal sequences.
    /// </summary>
    /// <exception cref="ArgumentException"><see cref="RelativePath"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="RelativePath"/> is rooted or contains path traversal sequences.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RelativePath))
            throw new ArgumentException("Relative path cannot be null, empty, or whitespace.", nameof(RelativePath));

        if (Path.IsPathRooted(RelativePath))
            throw new InvalidOperationException($"Relative path '{RelativePath}' must not be rooted.");

        var normalized = RelativePath.Replace('/', Path.DirectorySeparatorChar)
                                     .Replace('\\', Path.DirectorySeparatorChar);

        if (normalized.Contains($"..{Path.DirectorySeparatorChar}") || normalized.EndsWith(".."))
            throw new InvalidOperationException($"Relative path '{RelativePath}' must not contain traversal sequences.");
    }
}

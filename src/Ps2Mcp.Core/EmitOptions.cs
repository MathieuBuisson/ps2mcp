using System;
using System.IO;

namespace Ps2Mcp.Core;

/// <summary>
/// Configuration options passed to a server emitter.
/// </summary>
/// <param name="BundledModuleImportPath">
/// The relative import path to the bundled module (e.g. <c>./modules/Foo/Foo.psd1</c>).
/// May use <c>/</c> or <c>\</c> separators. Must not be rooted or contain traversal sequences.
/// </param>
public sealed record EmitOptions(string BundledModuleImportPath)
{
    /// <summary>
    /// Validates that <see cref="BundledModuleImportPath"/> is a non-empty, relative path without traversal sequences.
    /// </summary>
    /// <exception cref="ArgumentException"><see cref="BundledModuleImportPath"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="BundledModuleImportPath"/> is rooted or contains path traversal sequences.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BundledModuleImportPath))
            throw new ArgumentException("Bundled module import path cannot be null, empty, or whitespace.", nameof(BundledModuleImportPath));

        if (Path.IsPathRooted(BundledModuleImportPath))
            throw new InvalidOperationException($"Bundled module import path '{BundledModuleImportPath}' must not be rooted.");

        var normalized = BundledModuleImportPath.Replace('/', Path.DirectorySeparatorChar)
                                                .Replace('\\', Path.DirectorySeparatorChar);

        if (normalized.Contains($"..{Path.DirectorySeparatorChar}") || normalized.EndsWith(".."))
            throw new InvalidOperationException($"Bundled module import path '{BundledModuleImportPath}' must not contain traversal sequences.");
    }
}

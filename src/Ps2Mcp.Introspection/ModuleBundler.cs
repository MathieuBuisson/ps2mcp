using System;
using System.Collections.Generic;
using System.IO;

namespace Ps2Mcp.Introspection;

/// <summary>
/// Copies the files described by <see cref="ModuleDirectoryInfo"/> into
/// <c>outputDirectory/src/modules/moduleName</c>, creating the directory structure as needed.
/// </summary>
public static class ModuleBundler
{
    /// <summary>
    /// Bundles the discovered module files into the standard output directory layout.
    /// </summary>
    /// <remarks>
    /// The target bundle directory is deleted and recreated before copying to ensure
    /// deterministic output. Each file is copied with overwrite enabled.
    /// </remarks>
    /// <param name="moduleDirectoryInfo">Description of the module's directory and files.</param>
    /// <param name="moduleName">Name of the module; must be a single directory segment.</param>
    /// <param name="outputDirectory">Root output directory.</param>
    /// <returns>The full path of the bundled module directory.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="moduleDirectoryInfo"/> or its properties are null.</exception>
    /// <exception cref="ArgumentException"><paramref name="moduleName"/> is empty, whitespace, or contains path separators.</exception>
    /// <exception cref="InvalidOperationException">A listed file is rooted or escapes the module directory.</exception>
    /// <exception cref="FileNotFoundException">A file listed in discovery does not exist on disk.</exception>
    public static string Bundle(ModuleDirectoryInfo moduleDirectoryInfo, string moduleName, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(moduleDirectoryInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleDirectoryInfo.ModuleDirectory);
        ArgumentNullException.ThrowIfNull(moduleDirectoryInfo.Files);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (moduleName.Contains("..") ||
            moduleName.Contains('/') ||
            moduleName.Contains('\\') ||
            moduleName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                $"Module name must be a single directory segment without path separators or traversal sequences. Got '{moduleName}'.",
                nameof(moduleName));
        }

        var bundledModuleDirectory = Path.Combine(
            Path.GetFullPath(outputDirectory),
            "src",
            "modules",
            moduleName);

        if (Directory.Exists(bundledModuleDirectory))
        {
            Directory.Delete(bundledModuleDirectory, recursive: true);
        }

        Directory.CreateDirectory(bundledModuleDirectory);

        var fullModuleDirectory = Path.GetFullPath(moduleDirectoryInfo.ModuleDirectory);
        var fullBundleDirectory = Path.GetFullPath(bundledModuleDirectory);
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in moduleDirectoryInfo.Files)
        {
            if (Path.IsPathRooted(relativePath))
                throw new InvalidOperationException($"Relative path '{relativePath}' is rooted and cannot be bundled.");

            var normalizedRelativePath = NormalizeRelativePath(relativePath);

            var sourcePath = Path.GetFullPath(Path.Combine(fullModuleDirectory, normalizedRelativePath));
            var destinationPath = Path.GetFullPath(Path.Combine(fullBundleDirectory, normalizedRelativePath));

            EnsureWithinDirectory(sourcePath, fullModuleDirectory, "module directory");
            EnsureWithinDirectory(destinationPath, fullBundleDirectory, "bundle output directory");

            var destinationDirectory = Path.GetDirectoryName(destinationPath)
                ?? throw new InvalidOperationException($"Could not determine destination directory for '{destinationPath}'.");

            if (createdDirectories.Add(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    $"Module file listed in discovery was not found: {sourcePath}",
                    sourcePath);
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        return bundledModuleDirectory;
    }

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);

    private static void EnsureWithinDirectory(string resolvedPath, string baseDirectory, string description)
    {
        var separator = Path.DirectorySeparatorChar;
        var normalizedBase = baseDirectory.TrimEnd(separator) + separator;

        if (!resolvedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase) &&
            resolvedPath != baseDirectory)
        {
            throw new InvalidOperationException(
                $"Resolved path '{resolvedPath}' escapes {description} '{baseDirectory}'.");
        }
    }
}

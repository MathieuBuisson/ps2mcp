using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Ps2Mcp.Introspection;

public static partial class ModuleDirectoryDiscovery
{
    public static ModuleDirectoryInfo Discover(ResolvedModule module)
    {
        // The module root is the directory containing the manifest (.psd1) or entry point (.psm1).
        var moduleDirectory = Path.GetDirectoryName(module.ManifestPath)
            ?? throw new InvalidOperationException($"Could not determine module directory for '{module.ManifestPath}'.");

        var files = EnumerateFiles(moduleDirectory);
        return ExtractManifestReferences(module, moduleDirectory, files);
    }

    private static IReadOnlyList<string> EnumerateFiles(string directory)
    {
        var root = Path.GetFullPath(directory);
        var result = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            // Normalize separators to '/' for cross-platform determinism in manifests and bundling output.
            result.Add(NormalizeRelativePath(root, path));
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static string NormalizeRelativePath(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static ModuleDirectoryInfo ExtractManifestReferences(ResolvedModule module, string moduleDirectory, IReadOnlyList<string> files)
    {
        if (!string.Equals(Path.GetExtension(module.ManifestPath), ".psd1", StringComparison.OrdinalIgnoreCase))
        {
            return new ModuleDirectoryInfo(moduleDirectory, files, ManifestReferences.Empty, null);
        }

        string contents;
        try
        {
            contents = File.ReadAllText(module.ManifestPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException or DirectoryNotFoundException or NotSupportedException)
        {
            return new ModuleDirectoryInfo(
                moduleDirectory,
                files,
                ManifestReferences.Empty,
                $"Could not read manifest '{module.ManifestPath}': {ex.Message}");
        }

        var nestedModules = ExtractQuotedArrayField(contents, NestedModulesRegex());
        var fileList = ExtractQuotedArrayField(contents, FileListRegex());
        var requiredModules = ExtractRequiredModules(contents);
        return new ModuleDirectoryInfo(moduleDirectory, files, new ManifestReferences(nestedModules, fileList, requiredModules), null);
    }

    private static IReadOnlyList<string> ExtractQuotedArrayField(string manifestContents, Regex fieldRegex)
    {
        var match = fieldRegex.Match(manifestContents);
        if (!match.Success) return Array.Empty<string>();

        if (match.Groups[1].Success) return new[] { NormalizeManifestPath(match.Groups[1].Value) };
        if (match.Groups[2].Success) return new[] { NormalizeManifestPath(match.Groups[2].Value) };

        var body = match.Groups[3].Value;
        var result = new List<string>();
        foreach (Match itemMatch in QuotedStringRegex().Matches(body))
        {
            result.Add(NormalizeManifestPath(QuotedStringValue(itemMatch)));
        }
        return result;
    }

    private static string NormalizeManifestPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            return normalized[2..];
        }
        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            return normalized[1..];
        }
        return normalized;
    }

    private static IReadOnlyList<string> ExtractRequiredModules(string manifestContents)
    {
        var match = RequiredModulesFieldRegex().Match(manifestContents);
        if (!match.Success) return Array.Empty<string>();

        if (match.Groups[1].Success) return new[] { match.Groups[1].Value };
        if (match.Groups[2].Success) return new[] { match.Groups[2].Value };

        var body = match.Groups[3].Value;
        // Per-element parser: bare strings and hashtables (with ModuleName) both contribute, in source order; mixed arrays are valid in PowerShell.
        var names = new List<string>();
        foreach (Match m in RequiredModulesElementRegex().Matches(body))
        {
            if (m.Groups[1].Success || m.Groups[2].Success)
            {
                names.Add(QuotedStringValue(m));
            }
            else if (m.Groups[3].Success)
            {
                var nameMatch = ModuleNameRegex().Match(m.Groups[3].Value);
                if (nameMatch.Success)
                {
                    names.Add(QuotedStringValue(nameMatch));
                }
            }
        }
        return names;
    }

    // Field regexes use alternation '...' | "..." with disjoint body classes so a string containing the opposite quote type is captured correctly.
    [GeneratedRegex(@"(?<![\w])NestedModules\s*=\s*(?:'([^']*)'|""([^""]*)""|@\(([\s\S]*?)\))", RegexOptions.IgnoreCase)]
    private static partial Regex NestedModulesRegex();

    [GeneratedRegex(@"(?<![\w])FileList\s*=\s*(?:'([^']*)'|""([^""]*)""|@\(([\s\S]*?)\))", RegexOptions.IgnoreCase)]
    private static partial Regex FileListRegex();

    [GeneratedRegex(@"(?<![\w])RequiredModules\s*=\s*(?:'([^']*)'|""([^""]*)""|@\(([\s\S]*?)\))", RegexOptions.IgnoreCase)]
    private static partial Regex RequiredModulesFieldRegex();

    [GeneratedRegex(@"'([^']*)'|""([^""]*)""|@\{([^{}]*)\}")]
    private static partial Regex RequiredModulesElementRegex();

    [GeneratedRegex(@"'([^']*)'|""([^""]*)""")]
    private static partial Regex QuotedStringRegex();

    [GeneratedRegex(@"ModuleName\s*=\s*(?:'([^']*)'|""([^""]*)"")", RegexOptions.IgnoreCase)]
    private static partial Regex ModuleNameRegex();

    private static string QuotedStringValue(Match match) =>
        match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
}

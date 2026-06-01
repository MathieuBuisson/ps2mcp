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

        var nestedModules = ExtractQuotedArrayField(contents, "NestedModules");
        var fileList = ExtractQuotedArrayField(contents, "FileList");
        var requiredModules = ExtractRequiredModules(contents);
        return new ModuleDirectoryInfo(moduleDirectory, files, new ManifestReferences(nestedModules, fileList, requiredModules), null);
    }

    private static IReadOnlyList<string> ExtractQuotedArrayField(string manifestContents, string fieldName)
    {
        var match = fieldName switch
        {
            "NestedModules" => NestedModulesRegex().Match(manifestContents),
            "FileList" => FileListRegex().Match(manifestContents),
            _ => throw new ArgumentException($"Unsupported field '{fieldName}'.", nameof(fieldName)),
        };
        if (!match.Success) return Array.Empty<string>();

        if (match.Groups[1].Success) return new[] { match.Groups[1].Value };
        if (match.Groups[2].Success) return new[] { match.Groups[2].Value };

        var body = match.Groups[3].Value;
        var result = new List<string>();
        foreach (Match itemMatch in QuotedStringRegex().Matches(body))
        {
            result.Add(QuotedStringValue(itemMatch));
        }
        return result;
    }

    private static IReadOnlyList<string> ExtractRequiredModules(string manifestContents)
    {
        var match = RequiredModulesFieldRegex().Match(manifestContents);
        if (!match.Success) return Array.Empty<string>();

        if (match.Groups[1].Success) return new[] { match.Groups[1].Value };
        if (match.Groups[2].Success) return new[] { match.Groups[2].Value };

        var body = match.Groups[3].Value;
        // Hashtable form (body contains '@{'): only ModuleName = ... entries count, so unrelated quoted strings (ModuleVersion, Description, etc.) are not captured. Pure-string form: every quoted string is a module name.
        var extractor = body.Contains("@{", StringComparison.Ordinal) ? ModuleNameRegex() : QuotedStringRegex();
        var names = new List<string>();
        foreach (Match m in extractor.Matches(body))
        {
            names.Add(QuotedStringValue(m));
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

    [GeneratedRegex(@"'([^']*)'|""([^""]*)""")]
    private static partial Regex QuotedStringRegex();

    [GeneratedRegex(@"ModuleName\s*=\s*(?:'([^']*)'|""([^""]*)"")")]
    private static partial Regex ModuleNameRegex();

    private static string QuotedStringValue(Match match) =>
        match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
}

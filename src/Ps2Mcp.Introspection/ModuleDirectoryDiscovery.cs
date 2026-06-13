using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Language;

namespace Ps2Mcp.Introspection;

public static class ModuleDirectoryDiscovery
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

        if (!TryParseManifestData(contents, module.ManifestPath, out var manifestData))
        {
            return new ModuleDirectoryInfo(moduleDirectory, files, ManifestReferences.Empty, null);
        }

        var nestedModules = ExtractNormalizedPathValues(manifestData, "NestedModules");
        var fileList = ExtractNormalizedPathValues(manifestData, "FileList");
        var requiredModules = ExtractRequiredModules(manifestData);
        return new ModuleDirectoryInfo(moduleDirectory, files, new ManifestReferences(nestedModules, fileList, requiredModules), null);
    }

    private static bool TryParseManifestData(string manifestContents, string manifestPath, out IDictionary manifestData)
    {
        var ast = Parser.ParseInput(manifestContents, manifestPath, out _, out var errors);
        if (errors.Length > 0 || !TryGetManifestHashtableAst(ast, out var manifestHashtable))
        {
            manifestData = null!;
            return false;
        }

        try
        {
            if (manifestHashtable.SafeGetValue() is IDictionary dictionary)
            {
                manifestData = dictionary;
                return true;
            }
        }
        catch (InvalidOperationException)
        {
        }

        manifestData = null!;
        return false;
    }

    private static bool TryGetManifestHashtableAst(ScriptBlockAst ast, out HashtableAst manifestHashtable)
    {
        if (ast.EndBlock.Statements.Count == 1
            && ast.EndBlock.Statements[0] is PipelineAst { PipelineElements.Count: 1 } pipelineAst
            && pipelineAst.PipelineElements[0] is CommandExpressionAst { Expression: HashtableAst hashtableAst })
        {
            manifestHashtable = hashtableAst;
            return true;
        }

        manifestHashtable = null!;
        return false;
    }

    private static IReadOnlyList<string> ExtractNormalizedPathValues(IDictionary manifestData, string key)
    {
        if (!TryGetCaseInsensitiveValue(manifestData, key, out var value))
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        foreach (var item in EnumerateValues(value))
        {
            if (item is string path)
            {
                result.Add(NormalizeManifestPath(path));
            }
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

    private static IReadOnlyList<string> ExtractRequiredModules(IDictionary manifestData)
    {
        if (!TryGetCaseInsensitiveValue(manifestData, "RequiredModules", out var value))
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        foreach (var item in EnumerateValues(value))
        {
            if (item is string moduleName)
            {
                names.Add(moduleName);
            }
            else if (item is IDictionary moduleSpec
                && TryGetCaseInsensitiveValue(moduleSpec, "ModuleName", out var specModuleName)
                && specModuleName is string specModuleNameString)
            {
                names.Add(specModuleNameString);
            }
        }

        return names;
    }

    private static bool TryGetCaseInsensitiveValue(IDictionary dictionary, string key, out object? value)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is string entryKey
                && string.Equals(entryKey, key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static IEnumerable<object?> EnumerateValues(object? value)
    {
        switch (value)
        {
            case null:
                yield break;
            case string:
            case IDictionary:
                yield return value;
                yield break;
            case IEnumerable enumerable:
                foreach (var item in enumerable)
                {
                    yield return item;
                }
                yield break;
            default:
                yield return value;
                yield break;
        }
    }
}

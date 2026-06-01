using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Ps2Mcp.Introspection;

public static partial class ModuleInputResolver
{
    public static ModuleInputResolution Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ModuleInputResolution.Invalid("Module path is required.");
        }
        if (Directory.Exists(path))
        {
            return ModuleInputResolution.Invalid($"Module path '{path}' is a directory; expected a .psd1 or .psm1 file.");
        }
        if (!File.Exists(path))
        {
            return ModuleInputResolution.Invalid($"Module path '{path}' does not exist.");
        }

        var fullPath = Path.GetFullPath(path);
        var extension = Path.GetExtension(fullPath);
        if (!string.Equals(extension, ".psd1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".psm1", StringComparison.OrdinalIgnoreCase))
        {
            return ModuleInputResolution.Invalid($"Unsupported module extension '{extension}'; expected .psd1 or .psm1.");
        }

        var moduleName = Path.GetFileNameWithoutExtension(fullPath);

        if (string.Equals(extension, ".psm1", StringComparison.OrdinalIgnoreCase))
        {
            return ModuleInputResolution.Resolved(
                new ResolvedModule(fullPath, fullPath, moduleName, ModuleKind.Script));
        }

        string manifestContents;
        try
        {
            manifestContents = File.ReadAllText(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException or DirectoryNotFoundException or NotSupportedException)
        {
            return ModuleInputResolution.Invalid($"Could not read manifest '{fullPath}': {ex.Message}");
        }

        var rootModule = ExtractRootModule(manifestContents);
        if (rootModule is null)
        {
            return ModuleInputResolution.Invalid($"Manifest '{fullPath}' does not declare a RootModule.");
        }

        var manifestDir = Path.GetDirectoryName(fullPath)!;
        // RootModule is resolved relative to the manifest directory unless the value is already an absolute path.
        var entryPointPath = Path.IsPathRooted(rootModule)
            ? rootModule
            : Path.GetFullPath(Path.Combine(manifestDir, rootModule));
        if (!File.Exists(entryPointPath))
        {
            return ModuleInputResolution.Invalid($"Manifest '{fullPath}' references RootModule '{rootModule}' which does not exist at '{entryPointPath}'.");
        }
        // A .dll RootModule is a binary module; any other extension is treated as script.
        var kind = string.Equals(Path.GetExtension(entryPointPath), ".dll", StringComparison.OrdinalIgnoreCase)
            ? ModuleKind.Binary
            : ModuleKind.Script;
        return ModuleInputResolution.Resolved(
            new ResolvedModule(fullPath, entryPointPath, moduleName, kind));
    }

    // Matches 'RootModule = "X.psm1"' or unquoted 'RootModule = X.psm1'; unquoted form stops at whitespace or '#'.
    private const string RootModulePattern = @"^\s*RootModule\s*=\s*(?:['""]([^'""]+)['""]|([^\s#]+))";

    [GeneratedRegex(RootModulePattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex RootModuleRegex();

    private static string? ExtractRootModule(string manifestContents)
    {
        var match = RootModuleRegex().Match(manifestContents);
        if (!match.Success)
        {
            return null;
        }
        return match.Groups[1].Value.Length > 0 ? match.Groups[1].Value : match.Groups[2].Value;
    }
}

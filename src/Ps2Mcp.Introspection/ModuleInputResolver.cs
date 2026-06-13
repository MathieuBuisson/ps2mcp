using System;
using System.IO;
using System.Management.Automation.Language;

namespace Ps2Mcp.Introspection;

public static class ModuleInputResolver
{
    public static ModuleInputResolution Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ModuleInputResolution.Invalid("Module path is required.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ModuleInputResolution.Invalid($"Module path '{path}' is invalid: {ex.Message}");
        }

        if (Directory.Exists(fullPath))
        {
            return ModuleInputResolution.Invalid($"Module path '{fullPath}' is a directory; expected a .psd1 or .psm1 file.");
        }
        if (!File.Exists(fullPath))
        {
            return ModuleInputResolution.Invalid($"Module path '{fullPath}' does not exist.");
        }

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
                new ResolvedModule(fullPath, fullPath, moduleName, ModuleTypeClassifier.Classify(fullPath, fullPath)));
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

        var rootModuleExtraction = ExtractRootModule(manifestContents, fullPath);
        if (rootModuleExtraction.Diagnostic is not null)
        {
            return ModuleInputResolution.Invalid(rootModuleExtraction.Diagnostic);
        }
        if (!rootModuleExtraction.IsPresent)
        {
            return ModuleInputResolution.Invalid($"Manifest '{fullPath}' does not declare a RootModule.");
        }
        if (rootModuleExtraction.Value!.Length == 0)
        {
            return ModuleInputResolution.Invalid($"Manifest '{fullPath}' declares an empty RootModule, which is not supported.");
        }

        var rootModule = rootModuleExtraction.Value;
        var normalizedRootModule = NormalizeManifestPathSeparators(rootModule);

        var manifestDir = Path.GetDirectoryName(fullPath)!;
        var entryPointPath = Path.IsPathRooted(normalizedRootModule)
            ? normalizedRootModule
            : Path.GetFullPath(Path.Combine(manifestDir, normalizedRootModule));
        if (!File.Exists(entryPointPath))
        {
            return ModuleInputResolution.Invalid($"Manifest '{fullPath}' references RootModule '{rootModule}' which does not exist at '{entryPointPath}'.");
        }

        var entryPointExtension = Path.GetExtension(entryPointPath);
        if (!string.Equals(entryPointExtension, ".psm1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(entryPointExtension, ".dll", StringComparison.OrdinalIgnoreCase))
        {
            return ModuleInputResolution.Invalid(
                $"Manifest '{fullPath}' references RootModule '{rootModule}' with unsupported extension '{entryPointExtension}'; expected .psm1 or .dll.");
        }

        var kind = ModuleTypeClassifier.Classify(fullPath, entryPointPath);
        return ModuleInputResolution.Resolved(
            new ResolvedModule(fullPath, entryPointPath, moduleName, kind));
    }

    private static RootModuleExtraction ExtractRootModule(string manifestContents, string manifestPath)
    {
        var ast = Parser.ParseInput(manifestContents, manifestPath, out _, out var errors);
        if (errors.Length > 0)
        {
            return RootModuleExtraction.Invalid($"Manifest '{manifestPath}' could not be parsed: {errors[0].Message}");
        }

        if (!TryGetManifestHashtable(ast, out var manifestHashtable))
        {
            return RootModuleExtraction.Invalid($"Manifest '{manifestPath}' is not a valid PowerShell data file.");
        }

        foreach (var keyValuePair in manifestHashtable.KeyValuePairs)
        {
            if (keyValuePair.Item1 is not StringConstantExpressionAst keyAst
                || !string.Equals(keyAst.Value, "RootModule", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryReadScalarString(keyValuePair.Item2, out var rootModule))
            {
                return RootModuleExtraction.Invalid($"Manifest '{manifestPath}' declares a non-scalar RootModule value, which is not supported.");
            }

            return RootModuleExtraction.Found(rootModule);
        }

        return RootModuleExtraction.Missing();
    }

    private static string NormalizeManifestPathSeparators(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static bool TryGetManifestHashtable(ScriptBlockAst ast, out HashtableAst manifestHashtable)
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

    private static bool TryReadScalarString(StatementAst valueAst, out string value)
    {
        if (valueAst is not PipelineAst { PipelineElements.Count: 1 } pipelineAst)
        {
            value = null!;
            return false;
        }

        if (pipelineAst.PipelineElements[0] is CommandExpressionAst commandExpressionAst)
        {
            return TryReadStringExpression(commandExpressionAst.Expression, out value);
        }

        if (pipelineAst.PipelineElements[0] is CommandAst commandAst
            && commandAst.CommandElements.Count == 1
            && commandAst.GetCommandName() is { } commandName)
        {
            value = commandName;
            return true;
        }

        value = null!;
        return false;
    }

    private static bool TryReadStringExpression(ExpressionAst expressionAst, out string value)
    {
        switch (expressionAst)
        {
            case StringConstantExpressionAst stringConstantExpressionAst:
                value = stringConstantExpressionAst.Value;
                return true;
            case ExpandableStringExpressionAst expandableStringExpressionAst:
                value = expandableStringExpressionAst.Value;
                return true;
            case ConstantExpressionAst { Value: string stringValue }:
                value = stringValue;
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private readonly record struct RootModuleExtraction(bool IsPresent, string? Value, string? Diagnostic)
    {
        public static RootModuleExtraction Missing() => new(false, null, null);

        public static RootModuleExtraction Found(string value) => new(true, value, null);

        public static RootModuleExtraction Invalid(string diagnostic) => new(false, null, diagnostic);
    }
}

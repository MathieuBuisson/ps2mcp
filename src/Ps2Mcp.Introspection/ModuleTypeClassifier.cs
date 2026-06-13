using System;
using System.IO;

namespace Ps2Mcp.Introspection;

internal static class ModuleTypeClassifier
{
    public static ModuleKind Classify(string manifestPath, string entryPointPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new ArgumentException("Manifest path must not be null or whitespace.", nameof(manifestPath));
        }

        if (string.IsNullOrWhiteSpace(entryPointPath))
        {
            throw new ArgumentException("Entry point path must not be null or whitespace.", nameof(entryPointPath));
        }

        if (string.Equals(Path.GetExtension(manifestPath), ".psm1", StringComparison.OrdinalIgnoreCase))
        {
            return ModuleKind.Script;
        }

        return string.Equals(Path.GetExtension(entryPointPath), ".dll", StringComparison.OrdinalIgnoreCase)
            ? ModuleKind.Binary
            : ModuleKind.Script;
    }
}

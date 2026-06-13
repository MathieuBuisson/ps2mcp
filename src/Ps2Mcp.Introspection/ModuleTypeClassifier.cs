using System;
using System.IO;

namespace Ps2Mcp.Introspection;

internal static class ModuleTypeClassifier
{
    public static ModuleKind Classify(string manifestPath, string entryPointPath)
    {
        if (string.Equals(Path.GetExtension(manifestPath), ".psm1", StringComparison.OrdinalIgnoreCase))
        {
            return ModuleKind.Script;
        }

        return string.Equals(Path.GetExtension(entryPointPath), ".dll", StringComparison.OrdinalIgnoreCase)
            ? ModuleKind.Binary
            : ModuleKind.Script;
    }
}

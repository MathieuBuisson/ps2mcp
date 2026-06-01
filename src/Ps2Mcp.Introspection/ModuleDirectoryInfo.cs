using System.Collections.Generic;

namespace Ps2Mcp.Introspection;

public sealed record ModuleDirectoryInfo(
    string ModuleDirectory,
    IReadOnlyList<string> Files,
    ManifestReferences ManifestReferences,
    string? ManifestReadDiagnostic);

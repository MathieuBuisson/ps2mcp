namespace Ps2Mcp.Introspection;

public sealed record ResolvedModule(
    string ManifestPath,
    string EntryPointPath,
    string ModuleName,
    ModuleKind Kind);

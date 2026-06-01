namespace Ps2Mcp.Introspection;

public enum ModuleInputResolutionStatus
{
    Resolved,
    Invalid,
}

public sealed record ModuleInputResolution(
    ModuleInputResolutionStatus Status,
    ResolvedModule? Module,
    string? Diagnostic)
{
    public static ModuleInputResolution Resolved(ResolvedModule module) =>
        new(ModuleInputResolutionStatus.Resolved, module, null);

    public static ModuleInputResolution Invalid(string diagnostic) =>
        new(ModuleInputResolutionStatus.Invalid, null, diagnostic);
}

using System;
using System.Collections.Generic;

namespace Ps2Mcp.Introspection;

public sealed record ManifestReferences(
    IReadOnlyList<string> NestedModules,
    IReadOnlyList<string> FileList,
    IReadOnlyList<string> RequiredModules)
{
    public static ManifestReferences Empty { get; } = new(
        NestedModules: Array.Empty<string>(),
        FileList: Array.Empty<string>(),
        RequiredModules: Array.Empty<string>());
}

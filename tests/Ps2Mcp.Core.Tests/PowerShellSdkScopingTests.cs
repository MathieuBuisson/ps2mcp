using System.Reflection;
using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class PowerShellSdkScopingTests
{
    [Fact]
    public void Core_Assembly_DoesNotReferencePowerShellSdk()
    {
        // The package reference is scoped to Ps2Mcp.Introspection only. Core must stay free of any
        // transitive System.Management.Automation / Microsoft.PowerShell.* reference, or every consumer
        // (emitters, manifest writer) would inherit the SDK's AOT, trim, and dependency surface.
        var referenced = typeof(ModuleDefinition).Assembly.GetReferencedAssemblies();
        var names = referenced.Select(a => a.Name ?? string.Empty).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("System.Management.Automation", names);
        Assert.DoesNotContain(names, n => n.StartsWith("Microsoft.PowerShell.", StringComparison.Ordinal));
    }
}

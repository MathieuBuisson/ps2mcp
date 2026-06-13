using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Ps2Mcp.Introspection.Tests;

public sealed class BinaryModuleIntrospectorIntegrationTests
{
    private const string CrossPlatformModuleName = "Microsoft.PowerShell.Management";

    [WindowsOnlyFact]
    public void Introspect_RealPwshRun_AgainstBuiltinCrossPlatformBinaryModule_ProducesStableSubsetOnWindows()
    {
        // This stays Windows-only because this is Windows-runner coverage; the cross-platform module just provides a safe built-in fixture for that check.
        var runner = new PwshRunner();
        var modulePath = LocateBuiltinModulePath(runner);

        var result = BinaryModuleIntrospector.Introspect(
            new BinaryModuleIntrospectionRequest(modulePath, Timeout: TimeSpan.FromSeconds(30)),
            runner);

        Assert.Equal(CrossPlatformModuleName, result.Module.Name);
        Assert.NotEmpty(result.Tools);

        var getProcessMatches = result.Tools.Where(t => t.ToolName == "Get-Process").ToArray();
        Assert.True(
            getProcessMatches.Length == 1,
            $"Expected exactly one 'Get-Process' tool. Found {getProcessMatches.Length}. Available tools: [{string.Join(", ", result.Tools.Select(t => t.ToolName))}]");

        var getProcess = getProcessMatches[0];
        Assert.Equal("Get-Process", getProcess.SourceCommand);
        Assert.Contains(getProcess.Parameters, parameter => parameter.Name == "Name");
        Assert.Contains(getProcess.Parameters, parameter => parameter.Name == "Id");
        Assert.Contains(getProcess.Parameters, parameter => parameter.Name == "InputObject");
    }

    private static string LocateBuiltinModulePath(IPwshRunner runner)
    {
        var command = $$"""
            $module = Get-Module -ListAvailable -Name '{{CrossPlatformModuleName}}' |
                Select-Object -First 1 -ExpandProperty Path
            if (-not $module) { [Console]::Error.Write('Module not found.'); exit 1 }
            [Console]::Out.Write($module)
            """;

        var result = runner.Invoke(
            new PwshInvocation(
                "pwsh",
                new[] { "-NoProfile", "-NonInteractive", "-Command", command },
                TimeSpan.FromSeconds(30)));

        Assert.Equal(0, result.ExitCode);
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            Debug.WriteLine($"[ps2mcp] pwsh wrote to stderr while locating '{CrossPlatformModuleName}': {result.StandardError.Trim()}");
        }

        var modulePath = result.StandardOutput.Trim();
        Assert.False(string.IsNullOrWhiteSpace(modulePath));
        Assert.True(File.Exists(modulePath), $"Expected built-in module manifest at '{modulePath}'.");
        return modulePath;
    }
}

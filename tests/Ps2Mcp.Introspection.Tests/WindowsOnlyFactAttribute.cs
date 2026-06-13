using System;
using Xunit;

namespace Ps2Mcp.Introspection.Tests;

public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Integration test requires Windows (pwsh + Microsoft.PowerShell.Management).";
        }
    }
}
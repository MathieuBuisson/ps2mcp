using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Ps2Mcp.Cli.Tests;

internal static class CliProcess
{
    public static (int ExitCode, string StandardOutput, string StandardError) Run(params string[] args)
    {
        var cliExecutable = LocateCliExecutable();
        var startInfo = new ProcessStartInfo
        {
            FileName = cliExecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start CLI process.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
    }

    internal static string LocateCliExecutable()
    {
        var testAssemblyPath = typeof(CliProcess).Assembly.Location;
        var testDir = Path.GetDirectoryName(testAssemblyPath)!;
        var configName = Path.GetFileName(Path.GetDirectoryName(testDir)!);
        var solutionDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var cliExecutableDir = Path.Combine(solutionDir, "src", "Ps2Mcp.Cli", "bin", configName, "net10.0", RuntimeInformation.RuntimeIdentifier);
        var executableName = OperatingSystem.IsWindows() ? "ps2mcp.exe" : "ps2mcp";
        var cliExecutable = Path.Combine(cliExecutableDir, executableName);
        if (!File.Exists(cliExecutable))
        {
            throw new InvalidOperationException($"CLI executable not found at '{cliExecutable}'. Run 'dotnet build src/Ps2Mcp.Cli -c {configName}' before running integration tests.");
        }
        return cliExecutable;
    }
}

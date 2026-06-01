using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Ps2Mcp.Cli;

internal sealed class PwshRunner : IPwshRunner
{
    private const int ErrorFileNotFound = 2;

    public PwshInvocationResult Invoke(PwshInvocation invocation)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = invocation.Executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorFileNotFound)
        {
            throw new PwshStartException(PwshStartFailureKind.NotFound, invocation.Executable, ex);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            throw new PwshStartException(PwshStartFailureKind.Failed, invocation.Executable, ex);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return new PwshInvocationResult(
            process.ExitCode,
            stdoutTask.GetAwaiter().GetResult(),
            stderrTask.GetAwaiter().GetResult());
    }
}

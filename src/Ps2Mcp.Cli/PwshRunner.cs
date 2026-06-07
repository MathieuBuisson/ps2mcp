using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Ps2Mcp.Cli;

internal sealed class PwshRunner : IPwshRunner
{
    private const int ErrorFileNotFound = 2;

    private readonly IProcessRunner _processRunner;

    public PwshRunner()
        : this(new SystemProcessRunner())
    {
    }

    public PwshRunner(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

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

        IProcessHandle process;
        try
        {
            process = _processRunner.Start(startInfo);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorFileNotFound)
        {
            throw new PwshStartException(PwshStartFailureKind.NotFound, invocation.Executable, ex);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            throw new PwshStartException(PwshStartFailureKind.Failed, invocation.Executable, ex);
        }

        using (process)
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (invocation.Timeout is { } timeout)
            {
                if (!process.WaitForExit(timeout))
                {
                    // Process did not exit within the budget. Kill it, attempt to drain
                    // whatever it had buffered, then surface a structured exception so
                    // the orchestrator can decide how to report this (likely exit code 2
                    // with an actionable diagnostic).
                    process.Kill();
                    var partialStdout = TryReadResult(stdoutTask);
                    var partialStderr = TryReadResult(stderrTask);
                    throw new PwshTimeoutException(timeout, partialStdout, partialStderr, invocation.Executable);
                }
            }
            else
            {
                // No timeout: wait indefinitely. The CLI trusts the caller; a hung pwsh
                // will block the whole process, which is the correct behavior for callers
                // that explicitly opt out of a timeout.
                process.WaitForExit(System.Threading.Timeout.InfiniteTimeSpan);
            }

            return new PwshInvocationResult(
                process.ExitCode,
                stdoutTask.GetAwaiter().GetResult(),
                stderrTask.GetAwaiter().GetResult());
        }
    }

    private static string TryReadResult(Task<string> task)
    {
        // On the timeout path the streams may still be open when we try to read; the
        // preceding Kill() will close them, but the read task can race the close. We
        // do a best-effort drain and fall back to empty string if anything goes wrong.
        try
        {
            return task.GetAwaiter().GetResult();
        }
        catch
        {
            return string.Empty;
        }
    }
}

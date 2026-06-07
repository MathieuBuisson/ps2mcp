using System;
using System.Diagnostics;
using System.IO;

namespace Ps2Mcp.Cli;

internal sealed class SystemProcessRunner : IProcessRunner
{
    public IProcessHandle Start(ProcessStartInfo startInfo)
    {
        var process = Process.Start(startInfo);
        if (process is null)
        {
            // Process.Start returns null when no new process is started (for example, when
            // UseShellExecute=true and the verb is associated with an existing process).
            // We never set UseShellExecute, so this should not happen; surface it loudly.
            throw new InvalidOperationException("Process.Start returned null.");
        }
        return new SystemProcessHandle(process);
    }
}

internal sealed class SystemProcessHandle : IProcessHandle
{
    private readonly Process _process;

    public SystemProcessHandle(Process process)
    {
        _process = process;
    }

    public int ExitCode => _process.ExitCode;
    public TextReader StandardOutput => _process.StandardOutput;
    public TextReader StandardError => _process.StandardError;

    // WaitForExit(int) returns true if the process exited, false on timeout. The mapping
    // from TimeSpan to its int-milliseconds argument is isolated in ConvertToMilliseconds
    // so the edge cases (infinite, negative, overflow) are unit-testable without
    // spawning a real process. The -1 return value is a sentinel meaning "use the
    // parameterless Process.WaitForExit() for true infinite wait"; the underlying
    // Process.WaitForExit(int) would otherwise misinterpret -1 as a normal 0-ms timeout.
    public bool WaitForExit(TimeSpan timeout)
    {
        var ms = ConvertToMilliseconds(timeout);
        if (ms == -1)
        {
            _process.WaitForExit();
            return true;
        }
        return _process.WaitForExit(ms);
    }

    public void Kill() => _process.Kill();

    public void Dispose() => _process.Dispose();

    // Maps a TimeSpan to the int argument of Process.WaitForExit(int):
    //   - Timeout.InfiniteTimeSpan => -1 (caller must use parameterless WaitForExit)
    //   - Any other negative value => 0 (Process.WaitForExit's "return immediately" semantics
    //     for non-(-1) negatives; we surface this as an explicit clamp rather than passing
    //     a negative through to the OS)
    //   - Values exceeding int.MaxValue ms => int.MaxValue (clamped to int range)
    //   - Sub-millisecond finite timeouts (e.g. 0.5 ms) are truncated to 0 via the
    //     (int) cast; callers that need sub-ms precision should not be using this API
    //   - Otherwise, the value is returned as a whole-millisecond int
    internal static int ConvertToMilliseconds(TimeSpan timeout)
    {
        if (timeout == System.Threading.Timeout.InfiniteTimeSpan)
        {
            return -1;
        }
        var totalMs = timeout.TotalMilliseconds;
        if (totalMs < 0)
        {
            return 0;
        }
        if (totalMs > int.MaxValue)
        {
            return int.MaxValue;
        }
        return (int)totalMs;
    }
}

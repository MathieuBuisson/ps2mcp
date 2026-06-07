using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Ps2Mcp.Cli.Tests;

// Test double for IProcessRunner. Records the start calls and returns FakeProcessHandle
// instances whose stdout/stderr/exit code/timeout behavior are configured per-test via
// the OnStart callback (or the convenience factory methods).
internal sealed class FakeProcessRunner : IProcessRunner
{
    private Func<ProcessStartInfo, IProcessHandle>? onStart;

    public List<ProcessStartInfo> StartCalls { get; } = new();

    public Func<ProcessStartInfo, IProcessHandle> OnStart
    {
        get => onStart ?? throw new InvalidOperationException("FakeProcessRunner: OnStart was not configured.");
        set => onStart = value;
    }

    public IProcessHandle Start(ProcessStartInfo startInfo)
    {
        StartCalls.Add(startInfo);
        return OnStart(startInfo);
    }
}

// Test double for IProcessHandle. Canned stdout/stderr/exit code; can simulate a hung
// process via SimulateTimeout = true. Records every WaitForExit call's timeout and
// tracks whether Kill() and Dispose() were called.
internal sealed class FakeProcessHandle : IProcessHandle
{
    private readonly string stdout;
    private readonly string stderr;
    private readonly int exitCode;
    private readonly bool simulateTimeout;

    public FakeProcessHandle(string stdout, string stderr, int exitCode, bool simulateTimeout)
    {
        this.stdout = stdout;
        this.stderr = stderr;
        this.exitCode = exitCode;
        this.simulateTimeout = simulateTimeout;
    }

    public static FakeProcessHandle ForSuccess(string stdout = "", string stderr = "") =>
        new(stdout, stderr, 0, simulateTimeout: false);

    public static FakeProcessHandle ForFailure(int exitCode, string stdout = "", string stderr = "") =>
        new(stdout, stderr, exitCode, simulateTimeout: false);

    public static FakeProcessHandle ForTimeout() =>
        new(string.Empty, string.Empty, -1, simulateTimeout: true);

    public int ExitCode => exitCode;
    /// <summary>Records the timeout values passed to each WaitForExit call.</summary>
    public List<TimeSpan> WaitForExitCalls { get; } = new();
    public bool WasKilled { get; private set; }
    public bool WasDisposed { get; private set; }

    public TextReader StandardOutput => new StringReader(stdout);
    public TextReader StandardError => new StringReader(stderr);

    public bool WaitForExit(TimeSpan timeout)
    {
        WaitForExitCalls.Add(timeout);
        return !simulateTimeout;
    }

    public void Kill() => WasKilled = true;

    public void Dispose() => WasDisposed = true;
}

using System;
using System.ComponentModel;

namespace Ps2Mcp.Cli.Tests;

public sealed class PwshRunnerTests
{
    [Fact]
    public void Invoke_CapturesStandardOutput()
    {
        var handle = FakeProcessHandle.ForSuccess(stdout: "hello world");
        var (runner, _) = CreateRunner(handle);

        var result = runner.Invoke(MakeInvocation());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello world", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public void Invoke_CapturesStandardError()
    {
        var handle = FakeProcessHandle.ForFailure(exitCode: 1, stderr: "fatal: something went wrong");
        var (runner, _) = CreateRunner(handle);

        var result = runner.Invoke(MakeInvocation());

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal("fatal: something went wrong", result.StandardError);
    }

    [Fact]
    public void Invoke_CapturesExitCode()
    {
        var handle = FakeProcessHandle.ForFailure(exitCode: 42);
        var (runner, _) = CreateRunner(handle);

        var result = runner.Invoke(MakeInvocation());

        Assert.Equal(42, result.ExitCode);
    }

    [Fact]
    public void Invoke_CapturesBothStdoutAndStderrIndependently()
    {
        var handle = FakeProcessHandle.ForSuccess(stdout: "out-data", stderr: "err-data");
        var (runner, _) = CreateRunner(handle);

        var result = runner.Invoke(MakeInvocation());

        Assert.Equal("out-data", result.StandardOutput);
        Assert.Equal("err-data", result.StandardError);
    }

    [Fact]
    public void Invoke_PassesExecutableAndArgumentsVerbatimToProcess()
    {
        var handle = FakeProcessHandle.ForSuccess();
        var (runner, processRunner) = CreateRunner(handle);

        var invocation = new PwshInvocation(
            "pwsh",
            new[] { "-NoProfile", "-NonInteractive", "-Command", "Get-Process" });

        runner.Invoke(invocation);

        var startInfo = Assert.Single(processRunner.StartCalls);
        Assert.Equal("pwsh", startInfo.FileName);
        Assert.Equal(new[] { "-NoProfile", "-NonInteractive", "-Command", "Get-Process" }, startInfo.ArgumentList);
    }

    [Fact]
    public void Invoke_ConfiguresProcessForCapturedStdoutAndStderr()
    {
        var handle = FakeProcessHandle.ForSuccess();
        var (runner, processRunner) = CreateRunner(handle);

        runner.Invoke(MakeInvocation());

        var startInfo = Assert.Single(processRunner.StartCalls);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
    }

    [Fact]
    public void Invoke_DisposesProcessAfterCompletion()
    {
        var handle = FakeProcessHandle.ForSuccess();
        var (runner, _) = CreateRunner(handle);

        runner.Invoke(MakeInvocation());

        Assert.True(handle.WasDisposed);
        Assert.False(handle.WasKilled);
    }

    [Fact]
    public void Invoke_WithoutTimeout_WaitsIndefinitely()
    {
        var handle = FakeProcessHandle.ForSuccess();
        var (runner, _) = CreateRunner(handle);

        runner.Invoke(new PwshInvocation("pwsh", new[] { "-NoProfile" }));

        var timeout = Assert.Single(handle.WaitForExitCalls);
        Assert.Equal(System.Threading.Timeout.InfiniteTimeSpan, timeout);
    }

    [Fact]
    public void Invoke_WithTimeout_PassesTimeoutToWaitForExit()
    {
        var handle = FakeProcessHandle.ForSuccess();
        var (runner, _) = CreateRunner(handle);

        var invocation = new PwshInvocation("pwsh", new[] { "-NoProfile" }, TimeSpan.FromSeconds(30));

        runner.Invoke(invocation);

        var timeout = Assert.Single(handle.WaitForExitCalls);
        Assert.Equal(TimeSpan.FromSeconds(30), timeout);
    }

    [Fact]
    public void Invoke_ThrowsPwshStartExceptionNotFound_WhenProcessStartFailsWithWin32Error2()
    {
        var processRunner = new FakeProcessRunner
        {
            OnStart = _ => throw new Win32Exception(2, "The system cannot find the file specified"),
        };
        var runner = new PwshRunner(processRunner);

        var ex = Assert.Throws<PwshStartException>(() => runner.Invoke(MakeInvocation()));

        Assert.Equal(PwshStartFailureKind.NotFound, ex.Kind);
        Assert.NotNull(ex.InnerException);
        Assert.IsType<Win32Exception>(ex.InnerException);
        Assert.Contains("pwsh", ex.Message);
    }

    [Fact]
    public void Invoke_ThrowsPwshStartExceptionFailed_WhenProcessStartFailsWithOtherWin32Error()
    {
        var processRunner = new FakeProcessRunner
        {
            OnStart = _ => throw new Win32Exception(5, "Access is denied"),
        };
        var runner = new PwshRunner(processRunner);

        var ex = Assert.Throws<PwshStartException>(() => runner.Invoke(MakeInvocation()));

        Assert.Equal(PwshStartFailureKind.Failed, ex.Kind);
    }

    [Fact]
    public void Invoke_ThrowsPwshStartExceptionFailed_OnInvalidOperationException()
    {
        var processRunner = new FakeProcessRunner
        {
            OnStart = _ => throw new InvalidOperationException("Process.Start returned null."),
        };
        var runner = new PwshRunner(processRunner);

        var ex = Assert.Throws<PwshStartException>(() => runner.Invoke(MakeInvocation()));

        Assert.Equal(PwshStartFailureKind.Failed, ex.Kind);
    }

    [Fact]
    public void Invoke_ThrowsPwshTimeoutException_WhenProcessDoesNotExitWithinTimeout()
    {
        var handle = FakeProcessHandle.ForTimeout();
        var (runner, _) = CreateRunner(handle);

        var invocation = new PwshInvocation("pwsh", new[] { "-NoProfile" }, TimeSpan.FromMilliseconds(1));

        var ex = Assert.Throws<PwshTimeoutException>(() => runner.Invoke(invocation));

        Assert.Equal(TimeSpan.FromMilliseconds(1), ex.Timeout);
        Assert.Equal("pwsh", ex.Executable);
    }

    [Fact]
    public void Invoke_TimeoutException_IncludesPartialOutput()
    {
        // The fake's StringReader returns its contents immediately, so on the timeout
        // path the runner can drain the partial stdout/stderr into the exception.
        var handle = new FakeProcessHandle(stdout: "partial-output", stderr: "partial-error", exitCode: -1, simulateTimeout: true);
        var (runner, _) = CreateRunner(handle);

        var invocation = new PwshInvocation("pwsh", new[] { "-NoProfile" }, TimeSpan.FromMilliseconds(1));

        var ex = Assert.Throws<PwshTimeoutException>(() => runner.Invoke(invocation));

        Assert.Equal("partial-output", ex.PartialStandardOutput);
        Assert.Equal("partial-error", ex.PartialStandardError);
    }

    [Fact]
    public void Invoke_TimeoutKillsProcess()
    {
        var handle = FakeProcessHandle.ForTimeout();
        var (runner, _) = CreateRunner(handle);

        var invocation = new PwshInvocation("pwsh", new[] { "-NoProfile" }, TimeSpan.FromMilliseconds(1));

        Assert.Throws<PwshTimeoutException>(() => runner.Invoke(invocation));

        Assert.True(handle.WasKilled);
        Assert.True(handle.WasDisposed);
    }

    [Fact]
    public void Invoke_TimeoutExceptionMessage_IncludesTimeoutDuration()
    {
        var handle = FakeProcessHandle.ForTimeout();
        var (runner, _) = CreateRunner(handle);

        var invocation = new PwshInvocation("pwsh", new[] { "-NoProfile" }, TimeSpan.FromSeconds(2));

        var ex = Assert.Throws<PwshTimeoutException>(() => runner.Invoke(invocation));

        Assert.Contains("2", ex.Message);
        Assert.Contains("pwsh", ex.Message);
    }

    [Fact]
    public void Invoke_DoesNotInvokeKill_WhenProcessExitsBeforeTimeout()
    {
        var handle = FakeProcessHandle.ForSuccess();
        var (runner, _) = CreateRunner(handle);

        var invocation = new PwshInvocation("pwsh", new[] { "-NoProfile" }, TimeSpan.FromSeconds(30));

        runner.Invoke(invocation);

        Assert.False(handle.WasKilled);
        Assert.True(handle.WasDisposed);
    }

    private static (PwshRunner Runner, FakeProcessRunner ProcessRunner) CreateRunner(FakeProcessHandle handle)
    {
        var processRunner = new FakeProcessRunner { OnStart = _ => handle };
        return (new PwshRunner(processRunner), processRunner);
    }

    // Regression test for the production no-timeout path. The fake-based tests above
    // cannot catch this: FakeProcessHandle.WaitForExit always returns !simulateTimeout
    // regardless of the timeout argument, so they would pass even if SystemProcessHandle
    // mapped Timeout.InfiniteTimeSpan to a 0-ms wait. This test spawns a real short-lived
    // process through the concrete SystemProcessRunner and verifies that an invocation
    // without a timeout completes normally instead of being misinterpreted as a timeout
    // (which would surface as PwshTimeoutException on the production code path).
    [Fact]
    public void Invoke_NoTimeout_RealShortLivedProcess_DoesNotThrowTimeoutException()
    {
        var runner = new PwshRunner(new SystemProcessRunner());
        var executable = CliProcess.LocateCliExecutable();

        var result = runner.Invoke(new PwshInvocation(executable, new[] { "--version" }));

        Assert.Equal(0, result.ExitCode);
    }

    private static PwshInvocation MakeInvocation() =>
        new("pwsh", new[] { "-NoProfile", "-NonInteractive" });
}

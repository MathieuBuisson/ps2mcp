using System;

namespace Ps2Mcp.Cli.Tests;

public sealed class ExitCodeDispatcherTests
{
    [Fact]
    public void Dispatch_Success_ReturnsExitCodesSuccess()
    {
        var exitCode = ExitCodeDispatcher.Dispatch(CliOutcome.Success);

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    [Fact]
    public void Dispatch_Fatal_ReturnsExitCodesFatal()
    {
        var exitCode = ExitCodeDispatcher.Dispatch(CliOutcome.Fatal);

        Assert.Equal(ExitCodes.Fatal, exitCode);
    }

    [Fact]
    public void Dispatch_Drift_ReturnsExitCodesDrift()
    {
        var exitCode = ExitCodeDispatcher.Dispatch(CliOutcome.Drift);

        Assert.Equal(ExitCodes.Drift, exitCode);
    }

    [Fact]
    public void Dispatch_UnknownOutcome_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExitCodeDispatcher.Dispatch((CliOutcome)999));
    }
}

using System.ComponentModel;
using Ps2Mcp.Introspection;

namespace Ps2Mcp.Cli.Tests;

public sealed class PwshProbeTests
{
    private static readonly string[] ExpectedArguments = new[]
    {
        "-NoProfile",
        "-NonInteractive",
        "-Command",
        "$PSVersionTable.PSVersion.Major",
    };

    [Fact]
    public void Probe_InvokesPwshWithExpectedArguments()
    {
        var runner = new FakePwshRunner
        {
            OnInvoke = _ => new PwshInvocationResult(0, "7", string.Empty),
        };

        PwshProbe.Probe(runner);

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("pwsh", invocation.Executable);
        Assert.Equal(ExpectedArguments, invocation.Arguments);
    }

    [Fact]
    public void Probe_ReturnsOkWithVersion_WhenPwsh7IsPresent()
    {
        var runner = new FakePwshRunner
        {
            OnInvoke = _ => new PwshInvocationResult(0, "7", string.Empty),
        };

        var result = PwshProbe.Probe(runner);

        Assert.Equal(PwshProbeStatus.Ok, result.Status);
        Assert.Equal(7, result.MajorVersion);
        Assert.Null(result.DiagnosticMessage);
    }

    [Theory]
    [InlineData("7", 7)]
    [InlineData("8", 8)]
    [InlineData("9", 9)]
    public void Probe_ReturnsOkWithVersion_WhenPwshMajorVersionIsAtLeast7(string stdout, int expectedMajor)
    {
        var runner = new FakePwshRunner
        {
            OnInvoke = _ => new PwshInvocationResult(0, stdout, string.Empty),
        };

        var result = PwshProbe.Probe(runner);

        Assert.Equal(PwshProbeStatus.Ok, result.Status);
        Assert.Equal(expectedMajor, result.MajorVersion);
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("6", 6)]
    public void Probe_ReturnsUnsupportedVersion_WhenPwshMajorVersionIsBelow7(string stdout, int expectedMajor)
    {
        var runner = new FakePwshRunner
        {
            OnInvoke = _ => new PwshInvocationResult(0, stdout, string.Empty),
        };

        var result = PwshProbe.Probe(runner);

        Assert.Equal(PwshProbeStatus.UnsupportedVersion, result.Status);
        Assert.Equal(expectedMajor, result.MajorVersion);
        Assert.NotNull(result.DiagnosticMessage);
        Assert.Contains("7.x is required", result.DiagnosticMessage);
    }

    [Fact]
    public void Probe_ReturnsNotFound_WhenStartExceptionKindIsNotFound()
    {
        var inner = new Win32Exception(2, "The system cannot find the file specified");
        var runner = new FakePwshRunner
        {
            OnInvoke = _ => throw new PwshStartException(PwshStartFailureKind.NotFound, "pwsh", inner),
        };

        var result = PwshProbe.Probe(runner);

        Assert.Equal(PwshProbeStatus.NotFound, result.Status);
        Assert.Null(result.MajorVersion);
        Assert.NotNull(result.DiagnosticMessage);
        Assert.Contains("`pwsh`", result.DiagnosticMessage);
        Assert.Contains("not found", result.DiagnosticMessage);
        Assert.DoesNotContain("The system cannot find the file specified", result.DiagnosticMessage);
    }

    [Fact]
    public void Probe_ReturnsFailedWithInnerMessage_WhenStartExceptionKindIsFailed()
    {
        var inner = new Win32Exception(5, "Access is denied");
        var runner = new FakePwshRunner
        {
            OnInvoke = _ => throw new PwshStartException(PwshStartFailureKind.Failed, "pwsh", inner),
        };

        var result = PwshProbe.Probe(runner);

        Assert.Equal(PwshProbeStatus.Failed, result.Status);
        Assert.Null(result.MajorVersion);
        Assert.NotNull(result.DiagnosticMessage);
        Assert.Contains("Access is denied", result.DiagnosticMessage);
        Assert.DoesNotContain("not found", result.DiagnosticMessage);
    }

    [Fact]
    public void Probe_ReturnsFailed_WhenPwshExitsNonZero()
    {
        var runner = new FakePwshRunner
        {
            OnInvoke = _ => new PwshInvocationResult(1, string.Empty, "fatal error"),
        };

        var result = PwshProbe.Probe(runner);

        Assert.Equal(PwshProbeStatus.Failed, result.Status);
        Assert.Null(result.MajorVersion);
        Assert.NotNull(result.DiagnosticMessage);
        Assert.Contains("exit code 1", result.DiagnosticMessage);
        Assert.Contains("fatal error", result.DiagnosticMessage);
    }

    [Fact]
    public void Probe_ReturnsInvalidOutput_WhenPwshOutputIsNotAnInteger()
    {
        var runner = new FakePwshRunner
        {
            OnInvoke = _ => new PwshInvocationResult(0, "not a number", string.Empty),
        };

        var result = PwshProbe.Probe(runner);

        Assert.Equal(PwshProbeStatus.InvalidOutput, result.Status);
        Assert.Null(result.MajorVersion);
        Assert.NotNull(result.DiagnosticMessage);
        Assert.Contains("not a number", result.DiagnosticMessage);
    }

    [Fact]
    public void Probe_ReturnsInvalidOutput_WhenPwshOutputIsEmpty()
    {
        var runner = new FakePwshRunner
        {
            OnInvoke = _ => new PwshInvocationResult(0, string.Empty, string.Empty),
        };

        var result = PwshProbe.Probe(runner);

        Assert.Equal(PwshProbeStatus.InvalidOutput, result.Status);
        Assert.Null(result.MajorVersion);
    }
}

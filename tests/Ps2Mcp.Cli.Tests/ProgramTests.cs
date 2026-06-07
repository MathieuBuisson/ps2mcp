using System;
using System.ComponentModel;
using System.IO;
using Ps2Mcp.Introspection;

namespace Ps2Mcp.Cli.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void Run_WritesUsageToStandardOutputAndReturnsSuccessForHelpFlag()
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        using var runner = new FakePwshRunner();

        var exitCode = Program.Run(new[] { "verify", "-h" }, standardOutput, standardError, runner);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(CliArgumentsParser.UsageText + Environment.NewLine, standardOutput.ToString());
        Assert.Equal(string.Empty, standardError.ToString());
        Assert.Empty(runner.Invocations);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void Run_WritesVersionToStandardOutputAndReturnsSuccess(string flag)
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        using var runner = new FakePwshRunner();

        var exitCode = Program.Run(new[] { "build", flag }, standardOutput, standardError, runner);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(CliVersionProvider.DisplayVersion + Environment.NewLine, standardOutput.ToString());
        Assert.Equal(string.Empty, standardError.ToString());
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public void DisplayVersion_UsesSemanticVersionFormat()
    {
        Assert.Matches("^ps2mcp v\\d+\\.\\d+\\.\\d+$", CliVersionProvider.DisplayVersion);
    }

    [Fact]
    public void Run_ReturnsSuccessExitCodeForValidBuildInvocation()
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        using var runner = new FakePwshRunner
        {
            OnInvoke = _ => new PwshInvocationResult(0, "7", string.Empty),
        };

        var exitCode = Program.Run(new[] { "build", "module.psd1", "-t", "typescript", "-o", "dist-ts" }, standardOutput, standardError, runner);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.Equal(string.Empty, standardError.ToString());
        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("pwsh", invocation.Executable);
    }

    [Fact]
    public void Run_ReturnsSuccessExitCodeForValidVerifyInvocation()
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        using var runner = new FakePwshRunner
        {
            OnInvoke = _ => new PwshInvocationResult(0, "7", string.Empty),
        };

        var exitCode = Program.Run(new[] { "verify", "module.psd1", "-t", "python", "-o", "dist-py" }, standardOutput, standardError, runner);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Empty(standardError.ToString());
        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("pwsh", invocation.Executable);
    }

    [Fact]
    public void Run_ReturnsFatalExitCodeForUnknownCommand()
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        using var runner = new FakePwshRunner();

        var exitCode = Program.Run(new[] { "publish", "module.psd1", "-t", "typescript", "-o", "dist-ts" }, standardOutput, standardError, runner);

        Assert.Equal(ExitCodes.Fatal, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.Contains("Unknown command 'publish'", standardError.ToString());
        Assert.Contains(CliArgumentsParser.UsageText, standardError.ToString());
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public void Run_ReturnsFatalExitCodeAndWritesDiagnostic_WhenPreflightReportsNotFound()
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        using var runner = new FakePwshRunner
        {
            OnInvoke = _ => throw new PwshStartException(PwshStartFailureKind.NotFound, "pwsh", new Win32Exception(2)),
        };

        var exitCode = Program.Run(new[] { "build", "module.psd1", "-t", "typescript", "-o", "dist-ts" }, standardOutput, standardError, runner);

        Assert.Equal(ExitCodes.Fatal, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.Contains("`pwsh` 7.x is required but the executable was not found on PATH.", standardError.ToString());
        Assert.Single(runner.Invocations);
    }

    [Fact]
    public void Run_ReturnsFatalExitCodeAndWritesDiagnostic_WhenPreflightReportsUnsupportedVersion()
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        using var runner = new FakePwshRunner
        {
            OnInvoke = _ => new PwshInvocationResult(0, "5", string.Empty),
        };

        var exitCode = Program.Run(new[] { "verify", "module.psd1", "-t", "python", "-o", "dist-py" }, standardOutput, standardError, runner);

        Assert.Equal(ExitCodes.Fatal, exitCode);
        Assert.Contains("`pwsh` 7.x is required; found version 5.", standardError.ToString());
    }
}

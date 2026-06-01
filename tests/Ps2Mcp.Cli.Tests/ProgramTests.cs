using System;
using System.IO;

namespace Ps2Mcp.Cli.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void Run_WritesUsageToStandardOutputAndReturnsSuccessForHelpFlag()
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();

        var exitCode = Program.Run(new[] { "verify", "-h" }, standardOutput, standardError);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(CliArgumentsParser.UsageText + Environment.NewLine, standardOutput.ToString());
        Assert.Equal(string.Empty, standardError.ToString());
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void Run_WritesVersionToStandardOutputAndReturnsSuccess(string flag)
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();

        var exitCode = Program.Run(new[] { "build", flag }, standardOutput, standardError);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(CliVersionProvider.DisplayVersion + Environment.NewLine, standardOutput.ToString());
        Assert.Equal(string.Empty, standardError.ToString());
    }

    [Fact]
    public void DisplayVersion_UsesSemanticVersionFormat()
    {
        Assert.Matches("^ps2mcp v\\d+\\.\\d+\\.\\d+$", CliVersionProvider.DisplayVersion);
    }

    [Fact]
    public void Run_ReturnsSuccessExitCodeForValidInvocation()
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();

        var exitCode = Program.Run(new[] { "build", "module.psd1", "-t", "typescript", "-o", "dist-ts" }, standardOutput, standardError);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.Equal(string.Empty, standardError.ToString());
    }

    [Fact]
    public void Run_ReturnsFatalExitCodeForUnknownCommand()
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();

        var exitCode = Program.Run(new[] { "publish", "module.psd1", "-t", "typescript", "-o", "dist-ts" }, standardOutput, standardError);

        Assert.Equal(ExitCodes.Fatal, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.Contains("Unknown command 'publish'", standardError.ToString());
        Assert.Contains(CliArgumentsParser.UsageText, standardError.ToString());
    }
}

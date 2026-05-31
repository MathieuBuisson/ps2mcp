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

        Assert.Equal(0, exitCode);
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

        Assert.Equal(0, exitCode);
        Assert.Equal(CliVersionProvider.DisplayVersion + Environment.NewLine, standardOutput.ToString());
        Assert.Equal(string.Empty, standardError.ToString());
    }

    [Fact]
    public void DisplayVersion_UsesSemanticVersionFormat()
    {
        Assert.Matches("^ps2mcp v\\d+\\.\\d+\\.\\d+$", CliVersionProvider.DisplayVersion);
    }
}

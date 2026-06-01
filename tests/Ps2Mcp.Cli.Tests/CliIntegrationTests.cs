namespace Ps2Mcp.Cli.Tests;

public sealed class CliIntegrationTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Binary_HelpFlag_ExitsZeroAndShowsCommands(string flag)
    {
        var (exitCode, standardOutput, standardError) = CliProcess.Run(flag);

        Assert.Equal(0, exitCode);
        Assert.Contains("build", standardOutput);
        Assert.Contains("verify", standardOutput);
        Assert.Empty(standardError);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void Binary_VersionFlag_ExitsZeroAndShowsVersion(string flag)
    {
        var (exitCode, standardOutput, standardError) = CliProcess.Run(flag);

        Assert.Equal(0, exitCode);
        Assert.Matches("^ps2mcp v\\d+\\.\\d+\\.\\d+$", standardOutput.Trim());
        Assert.Empty(standardError);
    }
}

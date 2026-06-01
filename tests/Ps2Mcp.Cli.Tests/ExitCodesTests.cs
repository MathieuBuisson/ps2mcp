namespace Ps2Mcp.Cli.Tests;

public sealed class ExitCodesTests
{
    [Fact]
    public void Success_IsZero()
    {
        Assert.Equal(0, ExitCodes.Success);
    }

    [Fact]
    public void Fatal_IsOne()
    {
        Assert.Equal(1, ExitCodes.Fatal);
    }

    [Fact]
    public void Drift_IsTwo()
    {
        Assert.Equal(2, ExitCodes.Drift);
    }
}

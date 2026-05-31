using System;

namespace Ps2Mcp.Cli.Tests;

public sealed class CliArgumentsParserTests
{
    [Fact]
    public void TryParse_ParsesBuildInvocation()
    {
        var args = new[] { "build", "module.psd1", "--target", "typescript", "--out", "dist-ts" };

        var parsed = CliArgumentsParser.TryParse(args, out var parseResult, out var errorMessage);

        Assert.True(parsed);
        Assert.Null(errorMessage);
        Assert.NotNull(parseResult);
        Assert.Equal(CliParseResultKind.Invocation, parseResult.Kind);
        var invocation = Assert.IsType<CliInvocation>(parseResult.Invocation);
        Assert.Equal(CliCommand.Build, invocation.Command);
        Assert.Equal("module.psd1", invocation.ModulePath);
        Assert.Equal(GenerationTarget.TypeScript, invocation.Target);
        Assert.Equal("dist-ts", invocation.OutputDirectory);
    }

    [Fact]
    public void TryParse_ParsesVerifyInvocation()
    {
        var args = new[] { "verify", "module.psd1", "--target", "python", "--out", "dist-py" };

        var parsed = CliArgumentsParser.TryParse(args, out var parseResult, out var errorMessage);

        Assert.True(parsed);
        Assert.Null(errorMessage);
        Assert.NotNull(parseResult);
        Assert.Equal(CliParseResultKind.Invocation, parseResult.Kind);
        var invocation = Assert.IsType<CliInvocation>(parseResult.Invocation);
        Assert.Equal(CliCommand.Verify, invocation.Command);
        Assert.Equal("module.psd1", invocation.ModulePath);
        Assert.Equal(GenerationTarget.Python, invocation.Target);
        Assert.Equal("dist-py", invocation.OutputDirectory);
    }

    [Fact]
    public void TryParse_ParsesBuildInvocationWithShortOptions()
    {
        var args = new[] { "build", "module.psd1", "-t", "typescript", "-o", "dist-ts" };

        var parsed = CliArgumentsParser.TryParse(args, out var parseResult, out var errorMessage);

        Assert.True(parsed);
        Assert.Null(errorMessage);
        Assert.NotNull(parseResult);
        Assert.Equal(CliParseResultKind.Invocation, parseResult.Kind);
        var invocation = Assert.IsType<CliInvocation>(parseResult.Invocation);
        Assert.Equal(CliCommand.Build, invocation.Command);
        Assert.Equal("module.psd1", invocation.ModulePath);
        Assert.Equal(GenerationTarget.TypeScript, invocation.Target);
        Assert.Equal("dist-ts", invocation.OutputDirectory);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void TryParse_ReturnsHelpOutcomeBeforeOtherValidation(string flag)
    {
        var args = new[] { "build", flag };

        var parsed = CliArgumentsParser.TryParse(args, out var parseResult, out var errorMessage);

        Assert.True(parsed);
        Assert.Null(errorMessage);
        Assert.NotNull(parseResult);
        Assert.Equal(CliParseResultKind.Help, parseResult.Kind);
        Assert.Null(parseResult.Invocation);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void TryParse_ReturnsVersionOutcomeBeforeOtherValidation(string flag)
    {
        var args = new[] { "build", flag };

        var parsed = CliArgumentsParser.TryParse(args, out var parseResult, out var errorMessage);

        Assert.True(parsed);
        Assert.Null(errorMessage);
        Assert.NotNull(parseResult);
        Assert.Equal(CliParseResultKind.Version, parseResult.Kind);
        Assert.Null(parseResult.Invocation);
    }

    [Fact]
    public void TryParse_ReturnsErrorWhenCommandIsMissing()
    {
        var parsed = CliArgumentsParser.TryParse([], out var parseResult, out var errorMessage);

        AssertParseFailure(parsed, parseResult, errorMessage, "command", "required");
    }

    [Fact]
    public void TryParse_ReturnsErrorWhenTargetIsMissing()
    {
        var args = new[] { "build", "module.psd1", "--out", "dist-ts" };

        var parsed = CliArgumentsParser.TryParse(args, out var parseResult, out var errorMessage);

        AssertParseFailure(parsed, parseResult, errorMessage, "--target", "required");
    }

    [Fact]
    public void TryParse_ReturnsErrorWhenOutIsMissing()
    {
        var args = new[] { "build", "module.psd1", "--target", "python" };

        var parsed = CliArgumentsParser.TryParse(args, out var parseResult, out var errorMessage);

        AssertParseFailure(parsed, parseResult, errorMessage, "--out", "required");
    }

    [Fact]
    public void TryParse_ReturnsErrorForUnknownCommand()
    {
        var args = new[] { "publish", "module.psd1", "--target", "python", "--out", "dist-py" };

        var parsed = CliArgumentsParser.TryParse(args, out var parseResult, out var errorMessage);

        AssertParseFailure(parsed, parseResult, errorMessage, "Unknown command", "publish");
    }

    [Fact]
    public void TryParse_ReturnsErrorWhenTargetIsSpecifiedMoreThanOnce()
    {
        var args = new[] { "build", "module.psd1", "--target", "typescript", "--target", "python", "--out", "dist-ts" };

        var parsed = CliArgumentsParser.TryParse(args, out var parseResult, out var errorMessage);

        AssertParseFailure(parsed, parseResult, errorMessage, "--target", "exactly once");
    }

    [Theory]
    [InlineData("build|--target|typescript|--out|dist-ts", "module path|required")]
    [InlineData("build|module.psd1|--target", "--target|requires a value")]
    [InlineData("build|module.psd1|--target|typescript|--out", "--out|requires a value")]
    [InlineData("build|module.psd1|-t|-o|dist-ts", "-t|requires a value")]
    [InlineData("build|module.psd1|-t|typescript|-o", "-o|requires a value")]
    [InlineData("build|module.psd1|--weird|x|--target|typescript|--out|dist-ts", "Unknown option|--weird")]
    [InlineData("build|module.psd1|-x|typescript|-o|dist-ts", "Unknown option|-x")]
    [InlineData("build|module.psd1|--target|typescript|--out|dist-ts|--out|dist-py", "--out|exactly once")]
    [InlineData("build|module.psd1|--target|ruby|--out|dist-ts", "--target|typescript|python")]
    [InlineData("build|module.psd1|--target|TypeScript|--out|dist-ts", "--target|typescript|python")]
    [InlineData("build|module.psd1|--target|Python|--out|dist-py", "--target|typescript|python")]
    [InlineData("build|module.psd1|--target|PYTHON|--out|dist-py", "--target|typescript|python")]
    [InlineData("build|a.psd1|b.psd1|--target|typescript|--out|dist-ts", "Unexpected argument|b.psd1")]
    public void TryParse_ReturnsExpectedErrorForInvalidArguments(string serializedArgs, string expectedFragments)
    {
        var args = serializedArgs.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var fragments = expectedFragments.Split('|', StringSplitOptions.RemoveEmptyEntries);

        var parsed = CliArgumentsParser.TryParse(args, out var parseResult, out var errorMessage);

        AssertParseFailure(parsed, parseResult, errorMessage, fragments);
    }

    private static void AssertParseFailure(bool parsed, CliParseResult? parseResult, string? errorMessage, params string[] expectedFragments)
    {
        Assert.False(parsed);
        Assert.Null(parseResult);

        var actualErrorMessage = Assert.IsType<string>(errorMessage);
        foreach (var fragment in expectedFragments)
        {
            Assert.Contains(fragment, actualErrorMessage);
        }
    }
}

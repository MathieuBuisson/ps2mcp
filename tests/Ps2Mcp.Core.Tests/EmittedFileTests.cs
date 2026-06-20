using System;
using System.IO;
using System.Linq;

namespace Ps2Mcp.Core.Tests;

public sealed class EmittedFileTests
{
    [Fact]
    public void Validate_NullRelativePath_ThrowsArgumentException()
    {
        var file = new EmittedFile(null!, "content");
        var ex = Assert.Throws<ArgumentException>(() => file.Validate());
        Assert.Equal("RelativePath", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyRelativePath_ThrowsArgumentException(string relativePath)
    {
        var file = new EmittedFile(relativePath, "content");
        var ex = Assert.Throws<ArgumentException>(() => file.Validate());
        Assert.Equal("RelativePath", ex.ParamName);
    }

    [Fact]
    public void Validate_RootedPath_ThrowsInvalidOperationException()
    {
        var file = new EmittedFile("/etc/passwd", "content");
        Assert.Throws<InvalidOperationException>(() => file.Validate());
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("sub/../../escape.txt")]
    public void Validate_TraversalPath_ThrowsInvalidOperationException(string relativePath)
    {
        var file = new EmittedFile(relativePath, "content");
        Assert.Throws<InvalidOperationException>(() => file.Validate());
    }

    [Theory]
    [InlineData("src/index.ts")]
    [InlineData("src/sub/file.js")]
    public void Validate_ValidRelativePath_DoesNotThrow(string relativePath)
    {
        var file = new EmittedFile(relativePath, "content");
        file.Validate();
    }
}

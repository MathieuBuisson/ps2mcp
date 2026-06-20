using System;
using System.IO;

namespace Ps2Mcp.Core.Tests;

public sealed class EmitOptionsTests
{
    [Fact]
    public void Validate_NullImportPath_ThrowsArgumentException()
    {
        var options = new EmitOptions(null!);
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("BundledModuleImportPath", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyImportPath_ThrowsArgumentException(string importPath)
    {
        var options = new EmitOptions(importPath);
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("BundledModuleImportPath", ex.ParamName);
    }

    [Fact]
    public void Validate_RootedPath_ThrowsInvalidOperationException()
    {
        var options = new EmitOptions("/etc/passwd");
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("sub/../../escape.txt")]
    public void Validate_TraversalPath_ThrowsInvalidOperationException(string importPath)
    {
        var options = new EmitOptions(importPath);
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Theory]
    [InlineData("./modules/Demo.Module/Demo.Module.psd1")]
    [InlineData("src/modules/Foo/Foo.psm1")]
    public void Validate_ValidImportPath_DoesNotThrow(string importPath)
    {
        var options = new EmitOptions(importPath);
        options.Validate();
    }
}

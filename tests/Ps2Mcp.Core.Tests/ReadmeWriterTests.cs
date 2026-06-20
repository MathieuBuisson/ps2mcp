using System;
using System.IO;
using System.Text;

namespace Ps2Mcp.Core.Tests;

public sealed class ReadmeWriterTests : IDisposable
{
    private readonly string _tempDirectory;

    public ReadmeWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ps2mcp-readme-writer-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Write_NullRequiredModules_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ReadmeWriter.Write("DemoModule", null!, _tempDirectory));

        Assert.Equal("requiredModules", ex.ParamName);
    }

    [Fact]
    public void Write_NullModuleName_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ReadmeWriter.Write(null!, Array.Empty<string>(), _tempDirectory));

        Assert.Equal("moduleName", ex.ParamName);
    }

    [Fact]
    public void Write_NullOutputDirectory_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ReadmeWriter.Write("DemoModule", Array.Empty<string>(), null!));

        Assert.Equal("outputDirectory", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Write_EmptyModuleName_ThrowsArgumentException(string moduleName)
    {
        var ex = Assert.Throws<ArgumentException>(() => ReadmeWriter.Write(moduleName, Array.Empty<string>(), _tempDirectory));

        Assert.Equal("moduleName", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Write_EmptyOutputDirectory_ThrowsArgumentException(string outputDirectory)
    {
        var ex = Assert.Throws<ArgumentException>(() => ReadmeWriter.Write("DemoModule", Array.Empty<string>(), outputDirectory));

        Assert.Equal("outputDirectory", ex.ParamName);
    }

    [Fact]
    public void Write_NullRequiredModuleItem_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            ReadmeWriter.Write("DemoModule", new[] { "Pester", null!, "Az.Accounts" }, _tempDirectory));

        Assert.Equal("requiredModules", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Write_EmptyRequiredModuleItem_ThrowsArgumentException(string emptyItem)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ReadmeWriter.Write("DemoModule", new[] { "Pester", emptyItem }, _tempDirectory));

        Assert.Equal("requiredModules", ex.ParamName);
    }

    [Fact]
    public void Write_CreatesOutputDirectoryAndRendersRequiredModulesInSourceOrder()
    {
        var outputDirectory = Path.Combine(_tempDirectory, "nested", "out");
        var requiredModules = new[] { "Pester", "Az.Accounts", "PSReadLine" };

        var readmePath = ReadmeWriter.Write("DemoModule", requiredModules, outputDirectory);

        Assert.Equal(Path.Combine(Path.GetFullPath(outputDirectory), ReadmeWriter.FileName), readmePath);
        Assert.True(Directory.Exists(outputDirectory));
        Assert.True(File.Exists(readmePath));

        var text = File.ReadAllText(readmePath, Encoding.UTF8);
        var expected = "# DemoModule prerequisites\n\n"
            + "This generated package bundles the source PowerShell module but does not bundle upstream PowerShell dependencies.\n\n"
            + "Install these PowerShell modules before running the generated MCP server:\n\n"
            + "- Pester\n"
            + "- Az.Accounts\n"
            + "- PSReadLine\n";

        Assert.Equal(expected, text);
        Assert.DoesNotContain("\r", text);
    }

    [Fact]
    public void Write_WithoutRequiredModules_RendersNoAdditionalPrerequisitesMessage()
    {
        var readmePath = ReadmeWriter.Write("DemoModule", Array.Empty<string>(), _tempDirectory);

        var text = File.ReadAllText(readmePath, Encoding.UTF8);
        var expected = "# DemoModule prerequisites\n\n"
            + "This generated package bundles the source PowerShell module but does not bundle upstream PowerShell dependencies.\n\n"
            + "No additional PowerShell modules are declared via RequiredModules.\n";

        Assert.Equal(expected, text);
        Assert.DoesNotContain("\r", text);
    }

    [Fact]
    public void Write_RepeatedInvocationsProduceByteIdenticalOutput()
    {
        var requiredModules = new[] { "Az.Accounts", "Az.Compute" };

        var readmePath = ReadmeWriter.Write("DemoModule", requiredModules, _tempDirectory);
        var first = File.ReadAllBytes(readmePath);

        ReadmeWriter.Write("DemoModule", requiredModules, _tempDirectory);
        var second = File.ReadAllBytes(readmePath);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Write_UsesUtf8WithoutBom()
    {
        var readmePath = ReadmeWriter.Write("DemoModule", Array.Empty<string>(), _tempDirectory);
        var bytes = File.ReadAllBytes(readmePath);

        Assert.Equal('#', (char)bytes[0]);
        Assert.NotEqual(0xEF, bytes[0]);
    }

    [Fact]
    public void Write_ModuleNameWithBackslashes_EscapesToDoubleBackslash()
    {
        var readmePath = ReadmeWriter.Write("My\\Module", Array.Empty<string>(), _tempDirectory);

        var text = File.ReadAllText(readmePath, Encoding.UTF8);
        Assert.StartsWith("# My\\\\Module prerequisites\n", text);
    }

    [Fact]
    public void Write_RequiredModuleNameWithNewlines_StripsNewlines()
    {
        var readmePath = ReadmeWriter.Write("DemoModule", new[] { "Line1\nLine2" }, _tempDirectory);

        var text = File.ReadAllText(readmePath, Encoding.UTF8);
        Assert.Contains("- Line1Line2\n", text);
    }
}

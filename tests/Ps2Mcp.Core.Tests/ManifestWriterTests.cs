using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Ps2Mcp.Core.Tests;

public sealed class ManifestWriterTests : IDisposable
{
    private readonly string _tempDirectory;

    public ManifestWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ps2mcp-manifest-writer-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Write_NullManifest_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ManifestWriter.Write(null!, _tempDirectory));

        Assert.Equal("manifest", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Write_EmptyOutputDirectory_ThrowsArgumentException(string outputDirectory)
    {
        var manifest = ManifestFixtures.MakeDefault();
        var ex = Assert.Throws<ArgumentException>(() => ManifestWriter.Write(manifest, outputDirectory));

        Assert.Equal("outputDirectory", ex.ParamName);
    }

    [Fact]
    public void Write_CreatesOutputDirectoryAndWritesManifestJson()
    {
        var manifest = ManifestFixtures.MakeDefault();
        var outputDirectory = Path.Combine(_tempDirectory, "nested", "out");

        var manifestPath = ManifestWriter.Write(manifest, outputDirectory);

        Assert.Equal(Path.Combine(Path.GetFullPath(outputDirectory), ManifestWriter.FileName), manifestPath);
        Assert.True(Directory.Exists(outputDirectory));
        Assert.True(File.Exists(manifestPath));
        var deserialized = ManifestJsonSerializer.Deserialize(File.ReadAllBytes(manifestPath));
        Assert.Equal(manifest.Module, deserialized.Module);
        Assert.Equal(manifest.IrVersion, deserialized.IrVersion);
        Assert.Equal(manifest.ContentHash, deserialized.ContentHash);
        Assert.Equal(manifest.Tools.Length, deserialized.Tools.Length);
        Assert.Equal(manifest.Tools[0].ToolName, deserialized.Tools[0].ToolName);
    }

    [Fact]
    public void Write_EmitsByteIdenticalSerializerOutputAcrossRepeatedInvocations()
    {
        var manifest = ManifestFixtures.MakeDefault();

        var manifestPath = ManifestWriter.Write(manifest, _tempDirectory);
        var first = File.ReadAllBytes(manifestPath);

        ManifestWriter.Write(manifest, _tempDirectory);
        var second = File.ReadAllBytes(manifestPath);
        var expected = ManifestJsonSerializer.Serialize(manifest);

        Assert.Equal(expected, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Write_UsesStableKeyOrderingAndLfOnlyLineEndings()
    {
        var manifest = ManifestFixtures.MakeDefault();

        var manifestPath = ManifestWriter.Write(manifest, _tempDirectory);
        var bytes = File.ReadAllBytes(manifestPath);
        var text = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(bytes);

        Assert.DoesNotContain("\r", text);
        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(ManifestFixtures.GetJsonPropertyOrder<ManifestDefinition>(), names);
    }

    [Fact]
    public async Task WriteAsync_NullManifest_ThrowsArgumentNullException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => ManifestWriter.WriteAsync(null!, _tempDirectory));

        Assert.Equal("manifest", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task WriteAsync_EmptyOutputDirectory_ThrowsArgumentException(string outputDirectory)
    {
        var manifest = ManifestFixtures.MakeDefault();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => ManifestWriter.WriteAsync(manifest, outputDirectory));

        Assert.Equal("outputDirectory", ex.ParamName);
    }

    [Fact]
    public async Task WriteAsync_CreatesOutputDirectoryAndWritesManifestJson()
    {
        var manifest = ManifestFixtures.MakeDefault();
        var outputDirectory = Path.Combine(_tempDirectory, "nested-async", "out");

        var manifestPath = await ManifestWriter.WriteAsync(manifest, outputDirectory);

        Assert.Equal(Path.Combine(Path.GetFullPath(outputDirectory), ManifestWriter.FileName), manifestPath);
        Assert.True(Directory.Exists(outputDirectory));
        Assert.True(File.Exists(manifestPath));
        var deserialized = ManifestJsonSerializer.Deserialize(File.ReadAllBytes(manifestPath));
        Assert.Equal(manifest.Module, deserialized.Module);
        Assert.Equal(manifest.IrVersion, deserialized.IrVersion);
        Assert.Equal(manifest.ContentHash, deserialized.ContentHash);
        Assert.Equal(manifest.Tools.Length, deserialized.Tools.Length);
        Assert.Equal(manifest.Tools[0].ToolName, deserialized.Tools[0].ToolName);
    }

    [Fact]
    public async Task WriteAsync_ProducesSameBytesAsSyncOverload()
    {
        var manifest = ManifestFixtures.MakeDefault();

        var syncPath = ManifestWriter.Write(manifest, _tempDirectory);
        var syncBytes = File.ReadAllBytes(syncPath);

        var asyncPath = await ManifestWriter.WriteAsync(manifest, _tempDirectory);
        var asyncBytes = File.ReadAllBytes(asyncPath);

        Assert.Equal(syncBytes, asyncBytes);
    }
}

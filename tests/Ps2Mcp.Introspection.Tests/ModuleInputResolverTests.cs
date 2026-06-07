using Ps2Mcp.Introspection;

namespace Ps2Mcp.Introspection.Tests;

public sealed class ModuleInputResolverTests : IDisposable
{
    private readonly string _tempDir;

    public ModuleInputResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ps2mcp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_Psm1Path_ReturnsScriptModule()
    {
        var path = WriteFile("MyModule.psm1", "# script body");

        var result = ModuleInputResolver.Resolve(path);

        Assert.Equal(ModuleInputResolutionStatus.Resolved, result.Status);
        Assert.NotNull(result.Module);
        Assert.Null(result.Diagnostic);
        var module = result.Module!;
        Assert.Equal(Path.GetFullPath(path), module.ManifestPath);
        Assert.Equal(Path.GetFullPath(path), module.EntryPointPath);
        Assert.Equal("MyModule", module.ModuleName);
        Assert.Equal(ModuleKind.Script, module.Kind);
    }

    [Fact]
    public void Resolve_Psd1PathWithScriptRootModule_ReturnsScriptModule()
    {
        WriteFile("MyModule.psm1", "# script body");
        var manifest = WriteFile("MyModule.psd1", "RootModule = 'MyModule.psm1'" + Environment.NewLine);

        var result = ModuleInputResolver.Resolve(manifest);

        Assert.Equal(ModuleInputResolutionStatus.Resolved, result.Status);
        Assert.NotNull(result.Module);
        Assert.Null(result.Diagnostic);
        var module = result.Module!;
        Assert.Equal(Path.GetFullPath(manifest), module.ManifestPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "MyModule.psm1")), module.EntryPointPath);
        Assert.Equal("MyModule", module.ModuleName);
        Assert.Equal(ModuleKind.Script, module.Kind);
    }

    [Fact]
    public void Resolve_Psd1PathWithBinaryRootModule_ReturnsBinaryModule()
    {
        WriteFile("MyModule.dll", "fake-dll");
        var manifest = WriteFile("MyModule.psd1", "RootModule = 'MyModule.dll'" + Environment.NewLine);

        var result = ModuleInputResolver.Resolve(manifest);

        Assert.Equal(ModuleInputResolutionStatus.Resolved, result.Status);
        Assert.NotNull(result.Module);
        Assert.Null(result.Diagnostic);
        Assert.Equal(ModuleKind.Binary, result.Module!.Kind);
    }

    [Fact]
    public void Resolve_Psd1PathWithUnquotedRootModule_ReturnsScriptModule()
    {
        WriteFile("MyModule.psm1", "# script body");
        var manifest = WriteFile("MyModule.psd1", "RootModule = MyModule.psm1" + Environment.NewLine);

        var result = ModuleInputResolver.Resolve(manifest);

        Assert.Equal(ModuleInputResolutionStatus.Resolved, result.Status);
        Assert.NotNull(result.Module);
        Assert.Null(result.Diagnostic);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "MyModule.psm1")), result.Module!.EntryPointPath);
        Assert.Equal(ModuleKind.Script, result.Module.Kind);
    }

    [Fact]
    public void Resolve_Psd1PathWithRelativeRootModule_ResolvesRelativeToManifestDirectory()
    {
        WriteFile("MyModule.psm1", "# script body");
        var manifest = WriteFile("MyModule.psd1", "RootModule = '.\\MyModule.psm1'" + Environment.NewLine);

        var result = ModuleInputResolver.Resolve(manifest);

        Assert.Equal(ModuleInputResolutionStatus.Resolved, result.Status);
        Assert.NotNull(result.Module);
        Assert.Null(result.Diagnostic);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tempDir, "MyModule.psm1")), result.Module!.EntryPointPath);
    }

    [Fact]
    public void Resolve_Psd1PathWithoutRootModule_ReturnsInvalid()
    {
        var manifest = WriteFile("MyModule.psd1", "Description = 'no root module here'" + Environment.NewLine);

        var result = ModuleInputResolver.Resolve(manifest);

        Assert.Equal(ModuleInputResolutionStatus.Invalid, result.Status);
        Assert.Null(result.Module);
        Assert.Contains("RootModule", result.Diagnostic);
    }

    [Fact]
    public void Resolve_Psd1PathWithMissingRootModuleFile_ReturnsInvalid()
    {
        var manifest = WriteFile("MyModule.psd1", "RootModule = 'DoesNotExist.psm1'" + Environment.NewLine);

        var result = ModuleInputResolver.Resolve(manifest);

        Assert.Equal(ModuleInputResolutionStatus.Invalid, result.Status);
        Assert.Null(result.Module);
        Assert.Contains("DoesNotExist.psm1", result.Diagnostic);
        Assert.Contains(manifest, result.Diagnostic);
    }

    [Fact]
    public void Resolve_MissingFile_ReturnsInvalid()
    {
        var path = Path.Combine(_tempDir, "DoesNotExist.psd1");

        var result = ModuleInputResolver.Resolve(path);

        Assert.Equal(ModuleInputResolutionStatus.Invalid, result.Status);
        Assert.Null(result.Module);
        Assert.Contains("does not exist", result.Diagnostic);
    }

    [Fact]
    public void Resolve_DirectoryPath_ReturnsInvalid()
    {
        var result = ModuleInputResolver.Resolve(_tempDir);

        Assert.Equal(ModuleInputResolutionStatus.Invalid, result.Status);
        Assert.Null(result.Module);
        Assert.Contains("directory", result.Diagnostic);
    }

    [Fact]
    public void Resolve_UnsupportedExtension_ReturnsInvalid()
    {
        var path = WriteFile("script.ps1", "# powershell script");

        var result = ModuleInputResolver.Resolve(path);

        Assert.Equal(ModuleInputResolutionStatus.Invalid, result.Status);
        Assert.Null(result.Module);
        Assert.Contains("Unsupported", result.Diagnostic);
    }

    [Fact]
    public void Resolve_EmptyPath_ReturnsInvalid()
    {
        var result = ModuleInputResolver.Resolve(string.Empty);

        Assert.Equal(ModuleInputResolutionStatus.Invalid, result.Status);
        Assert.Null(result.Module);
    }

    private string WriteFile(string fileName, string contents)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, contents);
        return path;
    }
}

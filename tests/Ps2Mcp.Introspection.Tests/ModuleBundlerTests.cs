using System;
using System.IO;

namespace Ps2Mcp.Introspection.Tests;

public sealed class ModuleBundlerTests : IDisposable
{
    private readonly string _sourceRoot;
    private readonly string _outputRoot;

    public ModuleBundlerTests()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ps2mcp-bundler-{Guid.NewGuid():N}");
        _sourceRoot = Path.Combine(tempRoot, "source");
        _outputRoot = Path.Combine(tempRoot, "out");
        Directory.CreateDirectory(_sourceRoot);
    }

    public void Dispose()
    {
        var tempRoot = Directory.GetParent(_sourceRoot)?.FullName;
        if (tempRoot is not null && Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Bundle_SingleFilePsm1_CopiesStandaloneModuleIntoSrcModulesDirectory()
    {
        var psm1 = WriteSourceFile("Standalone.psm1", "function Get-Standalone { 'ok' }" + Environment.NewLine);
        var module = new ResolvedModule(psm1, psm1, "Standalone", ModuleKind.Script);
        var directoryInfo = ModuleDirectoryDiscovery.Discover(module);

        var bundledDirectory = ModuleBundler.Bundle(directoryInfo, module.ModuleName, _outputRoot);

        Assert.Equal(Path.Combine(Path.GetFullPath(_outputRoot), "src", "modules", "Standalone"), bundledDirectory);
        AssertFileCopied("Standalone", "Standalone.psm1");
    }

    [Fact]
    public void Bundle_ManifestWithRootModule_CopiesManifestAndEntryPoint()
    {
        var manifest = WriteSourceFile(
            "SimpleScriptModule.psd1",
            "@{ RootModule = 'SimpleScriptModule.psm1'; ModuleVersion = '1.0.0' }" + Environment.NewLine);
        var entryPoint = WriteSourceFile("SimpleScriptModule.psm1", "function Get-SimpleThing { 'ok' }" + Environment.NewLine);
        var module = new ResolvedModule(manifest, entryPoint, "SimpleScriptModule", ModuleKind.Script);
        var directoryInfo = ModuleDirectoryDiscovery.Discover(module);

        ModuleBundler.Bundle(directoryInfo, module.ModuleName, _outputRoot);

        AssertFileCopied("SimpleScriptModule", "SimpleScriptModule.psd1");
        AssertFileCopied("SimpleScriptModule", "SimpleScriptModule.psm1");
    }

    [Fact]
    public void Bundle_ManifestWithNestedModules_CopiesNestedModuleFiles()
    {
        var manifest = WriteSourceFile(
            "ModuleWithManifestRefs.psd1",
            "@{ RootModule = 'ModuleWithManifestRefs.psm1'; NestedModules = @('SubModule.psm1') }" + Environment.NewLine);
        var entryPoint = WriteSourceFile("ModuleWithManifestRefs.psm1", "function Get-Primary { 'ok' }" + Environment.NewLine);
        WriteSourceFile("SubModule.psm1", "function Get-Secondary { 'ok' }" + Environment.NewLine);
        var module = new ResolvedModule(manifest, entryPoint, "ModuleWithManifestRefs", ModuleKind.Script);
        var directoryInfo = ModuleDirectoryDiscovery.Discover(module);

        ModuleBundler.Bundle(directoryInfo, module.ModuleName, _outputRoot);

        AssertFileCopied("ModuleWithManifestRefs", "ModuleWithManifestRefs.psd1");
        AssertFileCopied("ModuleWithManifestRefs", "ModuleWithManifestRefs.psm1");
        AssertFileCopied("ModuleWithManifestRefs", "SubModule.psm1");
    }

    [Fact]
    public void Bundle_ManifestWithFileList_CopiesReferencedFiles()
    {
        var manifest = WriteSourceFile(
            "ModuleWithFiles.psd1",
            "@{ RootModule = 'ModuleWithFiles.psm1'; FileList = @('README.md', 'LICENSE') }" + Environment.NewLine);
        var entryPoint = WriteSourceFile("ModuleWithFiles.psm1", "function Get-Primary { 'ok' }" + Environment.NewLine);
        WriteSourceFile("README.md", "# docs" + Environment.NewLine);
        WriteSourceFile("LICENSE", "MIT" + Environment.NewLine);
        var module = new ResolvedModule(manifest, entryPoint, "ModuleWithFiles", ModuleKind.Script);
        var directoryInfo = ModuleDirectoryDiscovery.Discover(module);

        ModuleBundler.Bundle(directoryInfo, module.ModuleName, _outputRoot);

        AssertFileCopied("ModuleWithFiles", "ModuleWithFiles.psd1");
        AssertFileCopied("ModuleWithFiles", "ModuleWithFiles.psm1");
        AssertFileCopied("ModuleWithFiles", "README.md");
        AssertFileCopied("ModuleWithFiles", "LICENSE");
    }

    [Fact]
    public void Bundle_NullModuleDirectoryInfo_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            ModuleBundler.Bundle(null!, "ValidName", _outputRoot));

        Assert.Equal("moduleDirectoryInfo", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Bundle_EmptyModuleDirectory_ThrowsArgumentException(string moduleDirectory)
    {
        var info = new ModuleDirectoryInfo(
            moduleDirectory,
            Array.Empty<string>(),
            ManifestReferences.Empty,
            null);

        var ex = Assert.Throws<ArgumentException>(() =>
            ModuleBundler.Bundle(info, "ValidName", _outputRoot));

        Assert.Equal("moduleDirectoryInfo.ModuleDirectory", ex.ParamName);
    }

    [Fact]
    public void Bundle_NullFiles_ThrowsArgumentNullException()
    {
        var info = new ModuleDirectoryInfo(
            _sourceRoot,
            null!,
            ManifestReferences.Empty,
            null);

        var ex = Assert.Throws<ArgumentNullException>(() =>
            ModuleBundler.Bundle(info, "ValidName", _outputRoot));

        Assert.Equal("moduleDirectoryInfo.Files", ex.ParamName);
    }

    [Fact]
    public void Bundle_RelativePathWithTraversal_ThrowsInvalidOperationException()
    {
        var legit = WriteSourceFile("legit.txt", "content" + Environment.NewLine);
        var module = new ResolvedModule(legit, legit, "TraversalTest", ModuleKind.Script);
        var directoryInfo = ModuleDirectoryDiscovery.Discover(module);

        var maliciousInfo = new ModuleDirectoryInfo(
            directoryInfo.ModuleDirectory,
            new[] { "../etc/passwd" },
            directoryInfo.ManifestReferences,
            directoryInfo.ManifestReadDiagnostic);

        Assert.Throws<InvalidOperationException>(() =>
            ModuleBundler.Bundle(maliciousInfo, module.ModuleName, _outputRoot));
    }

    [Fact]
    public void Bundle_RootedPath_ThrowsInvalidOperationException()
    {
        var legit = WriteSourceFile("legit.txt", "content" + Environment.NewLine);
        var module = new ResolvedModule(legit, legit, "RootedTest", ModuleKind.Script);
        var directoryInfo = ModuleDirectoryDiscovery.Discover(module);

        var absolutePath = Path.Combine(_sourceRoot, "legit.txt");
        var maliciousInfo = new ModuleDirectoryInfo(
            directoryInfo.ModuleDirectory,
            new[] { absolutePath },
            directoryInfo.ManifestReferences,
            directoryInfo.ManifestReadDiagnostic);

        Assert.Throws<InvalidOperationException>(() =>
            ModuleBundler.Bundle(maliciousInfo, module.ModuleName, _outputRoot));
    }

    [Theory]
    [InlineData("../OtherModule")]
    [InlineData("Foo\\Bar")]
    [InlineData("Foo/Bar")]
    public void Bundle_ModuleNameWithTraversalOrSeparators_ThrowsArgumentException(string maliciousName)
    {
        var legit = WriteSourceFile("legit.txt", "content" + Environment.NewLine);
        var module = new ResolvedModule(legit, legit, "TestModule", ModuleKind.Script);
        var directoryInfo = ModuleDirectoryDiscovery.Discover(module);

        Assert.Throws<ArgumentException>(() =>
            ModuleBundler.Bundle(directoryInfo, maliciousName, _outputRoot));
    }

    [Fact]
    public void Bundle_ModuleNameWithInvalidFileNameChars_ThrowsArgumentException()
    {
        var legit = WriteSourceFile("legit.txt", "content" + Environment.NewLine);
        var module = new ResolvedModule(legit, legit, "TestModule", ModuleKind.Script);
        var directoryInfo = ModuleDirectoryDiscovery.Discover(module);

        Assert.Throws<ArgumentException>(() =>
            ModuleBundler.Bundle(directoryInfo, "Bad:Name", _outputRoot));
    }

    [Fact]
    public void Bundle_MissingSourceFile_ThrowsFileNotFoundException()
    {
        var legit = WriteSourceFile("legit.txt", "content" + Environment.NewLine);
        var module = new ResolvedModule(legit, legit, "MissingFileTest", ModuleKind.Script);
        var directoryInfo = ModuleDirectoryDiscovery.Discover(module);

        var missingInfo = new ModuleDirectoryInfo(
            directoryInfo.ModuleDirectory,
            new[] { "legit.txt", "nonexistent.txt" },
            directoryInfo.ManifestReferences,
            directoryInfo.ManifestReadDiagnostic);

        var ex = Assert.Throws<FileNotFoundException>(() =>
            ModuleBundler.Bundle(missingInfo, module.ModuleName, _outputRoot));

        Assert.Contains("nonexistent.txt", ex.Message);
    }

    [Fact]
    public void Bundle_PreExistingStaleFile_IsRemovedFromOutput()
    {
        var psm1 = WriteSourceFile("CleanModule.psm1", "function Get-Clean { 'ok' }" + Environment.NewLine);
        var module = new ResolvedModule(psm1, psm1, "CleanModule", ModuleKind.Script);
        var directoryInfo = ModuleDirectoryDiscovery.Discover(module);

        var firstBundle = ModuleBundler.Bundle(directoryInfo, module.ModuleName, _outputRoot);
        var staleFile = Path.Combine(firstBundle, "stale.txt");
        File.WriteAllText(staleFile, "old content");

        Assert.True(File.Exists(staleFile));

        ModuleBundler.Bundle(directoryInfo, module.ModuleName, _outputRoot);

        Assert.False(File.Exists(staleFile), $"Stale file '{staleFile}' should have been removed.");
    }

    [Fact]
    public void Bundle_FileListShrinksBetweenBundles_RemovedFileNoLongerExists()
    {
        var psm1 = WriteSourceFile("ShrinkModule.psm1", "function Get-Shrink { 'ok' }" + Environment.NewLine);
        WriteSourceFile("Extra.txt", "extra content" + Environment.NewLine);
        var module = new ResolvedModule(psm1, psm1, "ShrinkModule", ModuleKind.Script);
        var directoryInfo = ModuleDirectoryDiscovery.Discover(module);

        ModuleBundler.Bundle(directoryInfo, module.ModuleName, _outputRoot);

        var bundledPath = Path.Combine(_outputRoot, "src", "modules", "ShrinkModule", "Extra.txt");
        Assert.True(File.Exists(bundledPath), "Extra.txt should exist after first bundle.");

        var reducedInfo = new ModuleDirectoryInfo(
            directoryInfo.ModuleDirectory,
            new[] { "ShrinkModule.psm1" },
            directoryInfo.ManifestReferences,
            directoryInfo.ManifestReadDiagnostic);

        ModuleBundler.Bundle(reducedInfo, module.ModuleName, _outputRoot);

        Assert.False(File.Exists(bundledPath), "Extra.txt should be gone after re-bundle with reduced file list.");
    }

    private string WriteSourceFile(string relativePath, string content)
    {
        var path = Path.Combine(_sourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
        return path;
    }

    private void AssertFileCopied(string moduleName, string relativePath)
    {
        var sourcePath = Path.Combine(
            _sourceRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        var bundledPath = Path.Combine(
            _outputRoot, "src", "modules", moduleName,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(bundledPath), $"Expected bundled file '{bundledPath}' to exist.");
        Assert.Equal(File.ReadAllText(sourcePath), File.ReadAllText(bundledPath));
    }
}

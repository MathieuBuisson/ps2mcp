using System;
using System.IO;
using Ps2Mcp.Introspection;

namespace Ps2Mcp.Introspection.Tests;

public sealed class ModuleDirectoryDiscoveryTests : IDisposable
{
    private readonly string _tempDir;

    public ModuleDirectoryDiscoveryTests()
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
    public void Discover_Psd1Input_ReturnsManifestDirectoryAndAllFiles()
    {
        WriteFile("MyModule.psd1", "RootModule = 'MyModule.psm1'" + Environment.NewLine);
        WriteFile("MyModule.psm1", "# script body");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(Path.GetFullPath(_tempDir), info.ModuleDirectory);
        Assert.Equal(new[] { "MyModule.psd1", "MyModule.psm1" }, info.Files);
        Assert.Null(info.ManifestReadDiagnostic);
    }

    [Fact]
    public void Discover_Psm1Input_ReturnsEntryPointDirectoryAndAllFiles()
    {
        var psm1 = WriteFile("Standalone.psm1", "# script body");
        var resolved = MakeResolvedModule(psm1, psm1, "Standalone", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(Path.GetFullPath(_tempDir), info.ModuleDirectory);
        Assert.Equal(new[] { "Standalone.psm1" }, info.Files);
        Assert.Null(info.ManifestReadDiagnostic);
    }

    [Fact]
    public void Discover_RecursivelyEnumeratesFilesInSubdirectories()
    {
        WriteFile("MyModule.psd1", "RootModule = 'MyModule.psm1'" + Environment.NewLine);
        WriteFile("MyModule.psm1", "# script body");
        var subDir = Path.Combine(_tempDir, "private");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "Helper.ps1"), "# helper");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(3, info.Files.Count);
        Assert.Contains("MyModule.psd1", info.Files);
        Assert.Contains("MyModule.psm1", info.Files);
        Assert.Contains("private/Helper.ps1", info.Files);
    }

    [Fact]
    public void Discover_FilesAreSortedOrdinalRegardlessOfCreationOrder()
    {
        WriteFile("a.psm1", "");
        WriteFile("B.psm1", "");
        WriteFile("c.psd1", "");
        var psm1 = Path.Combine(_tempDir, "a.psm1");
        var resolved = MakeResolvedModule(psm1, psm1, "a", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        // Ordinal sort: uppercase letters sort before lowercase letters.
        Assert.Equal(new[] { "B.psm1", "a.psm1", "c.psd1" }, info.Files);
    }

    [Fact]
    public void Discover_DirectoryWithOnlyManifest_ReturnsManifestInFileList()
    {
        var manifest = WriteFile("MyModule.psd1", "RootModule = 'MyModule.psm1'" + Environment.NewLine);
        var resolved = MakeResolvedModule(manifest, manifest, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Single(info.Files);
        Assert.Contains("MyModule.psd1", info.Files);
    }

    [Fact]
    public void Discover_Psd1InputWithNestedModules_ReturnsNestedModuleReferences()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; NestedModules = @('SubModule.psm1') }");
        WriteFile("MyModule.psm1", "# main");
        WriteFile("SubModule.psm1", "# sub");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "SubModule.psm1" }, info.ManifestReferences.NestedModules);
    }

    [Fact]
    public void Discover_Psd1InputWithNestedModulesScalar_ReturnsNestedModuleReference()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; NestedModules = 'SubModule.psm1' }");
        WriteFile("MyModule.psm1", "# main");
        WriteFile("SubModule.psm1", "# sub");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "SubModule.psm1" }, info.ManifestReferences.NestedModules);
    }

    [Fact]
    public void Discover_Psd1InputWithFileList_ReturnsFileListReferences()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; FileList = @('README.md', 'LICENSE') }");
        WriteFile("MyModule.psm1", "# main");
        WriteFile("README.md", "# readme");
        WriteFile("LICENSE", "MIT");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "README.md", "LICENSE" }, info.ManifestReferences.FileList);
    }

    [Fact]
    public void Discover_Psd1InputWithBackslashInArrayNestedModule_NormalizesToForwardSlash()
    {
        // Windows-authored manifests commonly use '\' in NestedModules/FileList paths; normalize to match the '/' form of enumerated files.
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; NestedModules = @('Sub1.psm1', 'private\\Helper.psm1') }");
        WriteFile("MyModule.psm1", "# main");
        WriteFile("Sub1.psm1", "# sub1");
        var subDir = Path.Combine(_tempDir, "private");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "Helper.psm1"), "# helper");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Sub1.psm1", "private/Helper.psm1" }, info.ManifestReferences.NestedModules);
    }

    [Fact]
    public void Discover_Psd1InputWithDotSlashPrefixInArrayFileList_StripsPrefix()
    {
        // PowerShell authors sometimes prefix relative paths with './' or '.\' in manifest values; strip the prefix and normalize separators.
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; FileList = @('./README.md', '.\\LICENSE') }");
        WriteFile("MyModule.psm1", "# main");
        WriteFile("README.md", "# readme");
        WriteFile("LICENSE", "MIT");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "README.md", "LICENSE" }, info.ManifestReferences.FileList);
    }

    [Fact]
    public void Discover_Psd1InputWithBackslashInScalarNestedModule_NormalizesToForwardSlash()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; NestedModules = 'private\\Helper.psm1' }");
        WriteFile("MyModule.psm1", "# main");
        var subDir = Path.Combine(_tempDir, "private");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "Helper.psm1"), "# helper");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "private/Helper.psm1" }, info.ManifestReferences.NestedModules);
    }

    [Fact]
    public void Discover_Psd1InputWithRequiredModulesAsHashtables_ReturnsModuleNamesInSourceOrder()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = @(@{ ModuleName = 'Az.Accounts' }, @{ ModuleName = 'Az.Compute'; ModuleVersion = '5.0.0' }) }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Az.Accounts", "Az.Compute" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithRequiredModulesAsStringArray_ReturnsModuleNames()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = @('Az.Accounts', 'Az.Compute') }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Az.Accounts", "Az.Compute" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithRequiredModulesAsScalar_ReturnsModuleName()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = 'Az.Accounts' }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Az.Accounts" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psm1Input_ReturnsEmptyManifestReferences()
    {
        var psm1 = WriteFile("Standalone.psm1", "# main");
        var resolved = MakeResolvedModule(psm1, psm1, "Standalone", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Empty(info.ManifestReferences.NestedModules);
        Assert.Empty(info.ManifestReferences.FileList);
        Assert.Empty(info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithoutManifestReferences_ReturnsEmptyManifestReferences()
    {
        WriteFile("MyModule.psd1", "RootModule = 'MyModule.psm1'" + Environment.NewLine);
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Empty(info.ManifestReferences.NestedModules);
        Assert.Empty(info.ManifestReferences.FileList);
        Assert.Empty(info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_RequiredModulesPreserveManifestSourceOrder()
    {
        // Hashtable order matches array order; verify names come out in the same order, not alphabetically.
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = @(@{ ModuleName = 'Zzz.Last' }, @{ ModuleName = 'Aaa.First' }) }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Zzz.Last", "Aaa.First" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_FixtureSimpleScriptModule_DiscoversAllFiles()
    {
        var manifest = LocateFixture("SimpleScriptModule", "SimpleScriptModule.psd1");
        var entryPoint = LocateFixture("SimpleScriptModule", "SimpleScriptModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "SimpleScriptModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(2, info.Files.Count);
        Assert.Contains("SimpleScriptModule.psd1", info.Files);
        Assert.Contains("SimpleScriptModule.psm1", info.Files);
        Assert.Empty(info.ManifestReferences.NestedModules);
        Assert.Empty(info.ManifestReferences.FileList);
        Assert.Empty(info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_FixtureModuleWithManifestRefs_DiscoversAllFilesAndReferences()
    {
        var manifest = LocateFixture("ModuleWithManifestRefs", "ModuleWithManifestRefs.psd1");
        var entryPoint = LocateFixture("ModuleWithManifestRefs", "ModuleWithManifestRefs.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "ModuleWithManifestRefs", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(5, info.Files.Count);
        Assert.Contains("ModuleWithManifestRefs.psd1", info.Files);
        Assert.Contains("ModuleWithManifestRefs.psm1", info.Files);
        Assert.Contains("SubModule.psm1", info.Files);
        Assert.Contains("README.md", info.Files);
        Assert.Contains("LICENSE", info.Files);
        Assert.Equal(new[] { "SubModule.psm1" }, info.ManifestReferences.NestedModules);
        Assert.Equal(new[] { "README.md", "LICENSE" }, info.ManifestReferences.FileList);
        Assert.Equal(new[] { "Az.Accounts", "Az.Compute" }, info.ManifestReferences.RequiredModules);
        Assert.Null(info.ManifestReadDiagnostic);
    }

    [Fact]
    public void Discover_FixtureModuleWithSubdirectory_DiscoversRecursiveFiles()
    {
        var manifest = LocateFixture("ModuleWithSubdirectory", "ModuleWithSubdirectory.psd1");
        var entryPoint = LocateFixture("ModuleWithSubdirectory", "ModuleWithSubdirectory.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "ModuleWithSubdirectory", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(3, info.Files.Count);
        Assert.Contains("ModuleWithSubdirectory.psd1", info.Files);
        Assert.Contains("ModuleWithSubdirectory.psm1", info.Files);
        Assert.Contains("private/Helper.ps1", info.Files);
    }

    [Fact]
    public void Discover_FixtureStandalonePsm1_DiscoversAllFilesAndEmptyReferences()
    {
        var psm1 = LocateFixture("StandalonePsm1", "Standalone.psm1");
        var resolved = MakeResolvedModule(psm1, psm1, "Standalone", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Standalone.psm1" }, info.Files);
        Assert.Empty(info.ManifestReferences.NestedModules);
        Assert.Empty(info.ManifestReferences.FileList);
        Assert.Empty(info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithBinaryRootModule_DiscoversDllAndAllFiles()
    {
        WriteFile("BinaryModule.psd1", "RootModule = 'BinaryModule.dll'" + Environment.NewLine);
        WriteFile("BinaryModule.dll", "fake-dll");
        var manifest = Path.Combine(_tempDir, "BinaryModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "BinaryModule.dll");
        var resolved = MakeResolvedModule(manifest, entryPoint, "BinaryModule", ModuleKind.Binary);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(Path.GetFullPath(_tempDir), info.ModuleDirectory);
        Assert.Equal(new[] { "BinaryModule.dll", "BinaryModule.psd1" }, info.Files);
        Assert.Empty(info.ManifestReferences.NestedModules);
        Assert.Empty(info.ManifestReferences.FileList);
        Assert.Empty(info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_BinaryModuleWithManifestReferences_ExtractsAllReferences()
    {
        WriteFile("BinaryModule.psd1", "@{ RootModule = 'BinaryModule.dll'; NestedModules = @('Companion.psm1'); FileList = @('README.md', 'LICENSE', 'dependent/Helpers.ps1'); RequiredModules = @(@{ ModuleName = 'Microsoft.PowerShell.Archive' }) }");
        WriteFile("BinaryModule.dll", "fake-dll");
        WriteFile("Companion.psm1", "# companion");
        WriteFile("README.md", "# readme");
        WriteFile("LICENSE", "MIT");
        var depDir = Path.Combine(_tempDir, "dependent");
        Directory.CreateDirectory(depDir);
        File.WriteAllText(Path.Combine(depDir, "Helpers.ps1"), "# helpers");
        var manifest = Path.Combine(_tempDir, "BinaryModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "BinaryModule.dll");
        var resolved = MakeResolvedModule(manifest, entryPoint, "BinaryModule", ModuleKind.Binary);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Companion.psm1" }, info.ManifestReferences.NestedModules);
        Assert.Equal(new[] { "README.md", "LICENSE", "dependent/Helpers.ps1" }, info.ManifestReferences.FileList);
        Assert.Equal(new[] { "Microsoft.PowerShell.Archive" }, info.ManifestReferences.RequiredModules);
        Assert.Contains("BinaryModule.dll", info.Files);
        Assert.Contains("BinaryModule.psd1", info.Files);
        Assert.Contains("Companion.psm1", info.Files);
        Assert.Contains("README.md", info.Files);
        Assert.Contains("LICENSE", info.Files);
        Assert.Contains("dependent/Helpers.ps1", info.Files);
    }

    [Fact]
    public void Discover_BinaryModuleWithSubdirectory_RecursesAndNormalizesRelativePath()
    {
        WriteFile("BinaryModule.psd1", "RootModule = 'BinaryModule.dll'" + Environment.NewLine);
        WriteFile("BinaryModule.dll", "fake-dll");
        var depDir = Path.Combine(_tempDir, "runtimes", "linux", "lib");
        Directory.CreateDirectory(depDir);
        File.WriteAllText(Path.Combine(depDir, "Native.so"), "fake-native");
        var manifest = Path.Combine(_tempDir, "BinaryModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "BinaryModule.dll");
        var resolved = MakeResolvedModule(manifest, entryPoint, "BinaryModule", ModuleKind.Binary);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(3, info.Files.Count);
        Assert.Contains("BinaryModule.dll", info.Files);
        Assert.Contains("BinaryModule.psd1", info.Files);
        Assert.Contains("runtimes/linux/lib/Native.so", info.Files);
    }

    [Fact]
    public void Discover_FixtureBinaryModule_DiscoversAllFiles()
    {
        var manifest = LocateFixture("BinaryModule", "BinaryModule.psd1");
        var entryPoint = LocateFixture("BinaryModule", "BinaryModule.dll");
        var resolved = MakeResolvedModule(manifest, entryPoint, "BinaryModule", ModuleKind.Binary);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(4, info.Files.Count);
        Assert.Contains("BinaryModule.psd1", info.Files);
        Assert.Contains("BinaryModule.dll", info.Files);
        Assert.Contains("README.md", info.Files);
        Assert.Contains("LICENSE", info.Files);
        Assert.Empty(info.ManifestReferences.NestedModules);
        Assert.Empty(info.ManifestReferences.FileList);
        Assert.Empty(info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_FixtureBinaryModuleWithManifestRefs_DiscoversAllFilesAndReferences()
    {
        var manifest = LocateFixture("BinaryModuleWithManifestRefs", "BinaryModuleWithManifestRefs.psd1");
        var entryPoint = LocateFixture("BinaryModuleWithManifestRefs", "BinaryModuleWithManifestRefs.dll");
        var resolved = MakeResolvedModule(manifest, entryPoint, "BinaryModuleWithManifestRefs", ModuleKind.Binary);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(6, info.Files.Count);
        Assert.Contains("BinaryModuleWithManifestRefs.psd1", info.Files);
        Assert.Contains("BinaryModuleWithManifestRefs.dll", info.Files);
        Assert.Contains("BinaryModuleWithManifestRefs.psm1", info.Files);
        Assert.Contains("README.md", info.Files);
        Assert.Contains("LICENSE", info.Files);
        Assert.Contains("dependent/Helpers.ps1", info.Files);
        Assert.Equal(new[] { "BinaryModuleWithManifestRefs.psm1" }, info.ManifestReferences.NestedModules);
        Assert.Equal(new[] { "README.md", "LICENSE", "dependent/Helpers.ps1" }, info.ManifestReferences.FileList);
        Assert.Equal(new[] { "Microsoft.PowerShell.Archive" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithDoubleQuotedScalarNestedModules_ReturnsNestedModuleReference()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; NestedModules = \"SubModule.psm1\" }");
        WriteFile("MyModule.psm1", "# main");
        WriteFile("SubModule.psm1", "# sub");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "SubModule.psm1" }, info.ManifestReferences.NestedModules);
    }

    [Fact]
    public void Discover_Psd1InputWithDoubleQuotedScalarFileList_ReturnsFileListReference()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; FileList = \"README.md\" }");
        WriteFile("MyModule.psm1", "# main");
        WriteFile("README.md", "# readme");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "README.md" }, info.ManifestReferences.FileList);
    }

    [Fact]
    public void Discover_Psd1InputWithDoubleQuotedScalarRequiredModules_ReturnsModuleName()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = \"Az.Accounts\" }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Az.Accounts" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithDoubleQuotedArrayNestedModules_ReturnsNestedModuleReferences()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; NestedModules = @(\"Sub1.psm1\", \"Sub2.psd1\") }");
        WriteFile("MyModule.psm1", "# main");
        WriteFile("Sub1.psm1", "# sub1");
        WriteFile("Sub2.psd1", "# sub2");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Sub1.psm1", "Sub2.psd1" }, info.ManifestReferences.NestedModules);
    }

    [Fact]
    public void Discover_Psd1InputWithDoubleQuotedArrayFileList_ReturnsFileListReferences()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; FileList = @(\"README.md\", \"LICENSE\") }");
        WriteFile("MyModule.psm1", "# main");
        WriteFile("README.md", "# readme");
        WriteFile("LICENSE", "MIT");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "README.md", "LICENSE" }, info.ManifestReferences.FileList);
    }

    [Fact]
    public void Discover_Psd1InputWithDoubleQuotedStringArrayRequiredModules_ReturnsModuleNames()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = @(\"Az.Accounts\", \"Az.Compute\") }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Az.Accounts", "Az.Compute" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithDoubleQuotedHashtableRequiredModules_ReturnsModuleNames()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = @(@{ ModuleName = \"Az.Accounts\" }, @{ ModuleName = \"Az.Compute\"; ModuleVersion = \"5.0.0\" }) }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Az.Accounts", "Az.Compute" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithMixedQuotedManifest_ReturnsAllReferences()
    {
        // Mix single- and double-quoted values across all three fields in a single inline manifest.
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; NestedModules = @('Sub1.psm1', \"Sub2.psd1\"); FileList = @(\"README.md\", 'LICENSE'); RequiredModules = @(@{ ModuleName = 'Az.Accounts' }, @{ ModuleName = \"Az.Compute\" }) }");
        WriteFile("MyModule.psm1", "# main");
        WriteFile("Sub1.psm1", "# sub1");
        WriteFile("Sub2.psd1", "# sub2");
        WriteFile("README.md", "# readme");
        WriteFile("LICENSE", "MIT");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Sub1.psm1", "Sub2.psd1" }, info.ManifestReferences.NestedModules);
        Assert.Equal(new[] { "README.md", "LICENSE" }, info.ManifestReferences.FileList);
        Assert.Equal(new[] { "Az.Accounts", "Az.Compute" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithDoubleQuotedStringContainingSingleQuote_CapturesFullString()
    {
        // Manifest values can legitimately contain the opposite quote type; ensure the body class is disjoint.
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = \"It's.Required\" }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "It's.Required" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithSingleQuotedStringContainingDoubleQuote_CapturesFullString()
    {
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; FileList = @('He said \"hi\".md') }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "He said \"hi\".md" }, info.ManifestReferences.FileList);
    }

    [Fact]
    public void Discover_FixtureDoubleQuotedManifest_DiscoversAllFilesAndReferences()
    {
        var manifest = LocateFixture("ModuleWithDoubleQuotedManifest", "ModuleWithDoubleQuotedManifest.psd1");
        var entryPoint = LocateFixture("ModuleWithDoubleQuotedManifest", "ModuleWithDoubleQuotedManifest.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "ModuleWithDoubleQuotedManifest", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(5, info.Files.Count);
        Assert.Contains("ModuleWithDoubleQuotedManifest.psd1", info.Files);
        Assert.Contains("ModuleWithDoubleQuotedManifest.psm1", info.Files);
        Assert.Contains("SubModule.psm1", info.Files);
        Assert.Contains("README.md", info.Files);
        Assert.Contains("LICENSE", info.Files);
        Assert.Equal(new[] { "SubModule.psm1" }, info.ManifestReferences.NestedModules);
        Assert.Equal(new[] { "README.md", "LICENSE" }, info.ManifestReferences.FileList);
        Assert.Equal(new[] { "Az.Accounts", "Az.Compute" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithRequiredModulesAsHashtableWithUnrelatedQuotedStrings_DoesNotCaptureThem()
    {
        // The hashtable contains ModuleVersion and Description values; only the explicit ModuleName is a real module name.
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = @(@{ ModuleName = 'Az.Accounts'; ModuleVersion = '2.0.0'; Description = 'Azure Accounts module' }) }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Az.Accounts" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithHashtableArrayMissingModuleName_ReturnsEmpty()
    {
        // Hashtable form without any ModuleName entries must not fall through to the bare-string extractor.
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = @(@{ ModuleVersion = '1.0.0' }, @{ Description = 'no module name here' }) }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Empty(info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithMixedStringAndHashtableArray_CapturesAllElementsInSourceOrder()
    {
        // Mixed arrays are valid in PowerShell: bare strings are module names, hashtables contribute their ModuleName, and source order is preserved.
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = @('Pester', @{ ModuleName = 'Az.Accounts' }) }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Pester", "Az.Accounts" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithInterleavedHashtablesAndStrings_PreservesSourceOrder()
    {
        // Each element is classified independently; hashtable-string-hashtable must come out in that order, not grouped.
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = @(@{ ModuleName = 'Az.Accounts' }, 'Pester', @{ ModuleName = 'PSReadLine' }) }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Az.Accounts", "Pester", "PSReadLine" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithHashtableArrayMissingModuleName_SkipsHashtableButKeepsStrings()
    {
        // Mixed array with one hashtable that has no ModuleName (silently skipped) and one bare string (kept as module name).
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = @('Pester', @{ ModuleVersion = '1.0.0' }) }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Pester" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithLowercaseModuleNameKey_StillCapturesModuleName()
    {
        // PowerShell manifest keys are case-insensitive; ModuleNameRegex must honor that to match the surrounding field regex behavior.
        WriteFile("MyModule.psd1", "@{ RootModule = 'MyModule.psm1'; RequiredModules = @(@{ modulename = 'Az.Accounts' }) }");
        WriteFile("MyModule.psm1", "# main");
        var manifest = Path.Combine(_tempDir, "MyModule.psd1");
        var entryPoint = Path.Combine(_tempDir, "MyModule.psm1");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(new[] { "Az.Accounts" }, info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithUnreadableManifest_ReturnsDiagnosticAndEmptyReferences()
    {
        // The manifest path does not exist; the resolver is bypassed in this test, simulating a TOCTOU race or external manifest mutation.
        var manifest = Path.Combine(_tempDir, "Locked.psd1");
        var resolved = MakeResolvedModule(manifest, manifest, "Locked", ModuleKind.Script);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.Equal(Path.GetFullPath(_tempDir), info.ModuleDirectory);
        Assert.Empty(info.Files);
        Assert.NotNull(info.ManifestReadDiagnostic);
        Assert.Contains("Could not read manifest", info.ManifestReadDiagnostic);
        Assert.Contains("Locked.psd1", info.ManifestReadDiagnostic);
        Assert.Empty(info.ManifestReferences.NestedModules);
        Assert.Empty(info.ManifestReferences.FileList);
        Assert.Empty(info.ManifestReferences.RequiredModules);
    }

    [Fact]
    public void Discover_Psd1InputWithLockedManifest_ReturnsDiagnostic()
    {
        // Hold an exclusive lock on the manifest file to force File.ReadAllText to fail; on Unix this is advisory and may not throw, so the assertion is gated on Windows.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var manifest = WriteFile("MyModule.psd1", "RootModule = 'MyModule.psm1'" + Environment.NewLine);
        var entryPoint = WriteFile("MyModule.psm1", "# main");
        var resolved = MakeResolvedModule(manifest, entryPoint, "MyModule", ModuleKind.Script);
        using var lockHandle = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.None);

        var info = ModuleDirectoryDiscovery.Discover(resolved);

        Assert.NotNull(info.ManifestReadDiagnostic);
        Assert.Contains("Could not read manifest", info.ManifestReadDiagnostic);
        Assert.Empty(info.ManifestReferences.NestedModules);
    }

    private string WriteFile(string fileName, string contents)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    private static ResolvedModule MakeResolvedModule(string manifestPath, string entryPointPath, string moduleName, ModuleKind kind) =>
        new(manifestPath, entryPointPath, moduleName, kind);

    private static string LocateFixture(string moduleDir, string fileName)
    {
        // AppContext.BaseDirectory → tests/Ps2Mcp.Introspection.Tests/bin/Debug/net10.0/
        // Walking up four levels lands at tests/, then into fixtures/modules/<moduleDir>/<fileName>.
        var baseDir = AppContext.BaseDirectory;
        var testsRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var fixturePath = Path.Combine(testsRoot, "fixtures", "modules", moduleDir, fileName);
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException($"Fixture not found: {fixturePath}");
        }
        return fixturePath;
    }
}

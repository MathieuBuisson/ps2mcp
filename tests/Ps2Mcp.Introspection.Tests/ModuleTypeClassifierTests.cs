using Ps2Mcp.Introspection;

namespace Ps2Mcp.Introspection.Tests;

public sealed class ModuleTypeClassifierTests : IDisposable
{
    private readonly string _tempDir;

    public ModuleTypeClassifierTests()
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
    public void Classify_Psm1Input_ReturnsScript()
    {
        var modulePath = WriteFile("MyModule.psm1", "# script body");

        var kind = ModuleTypeClassifier.Classify(modulePath, modulePath);

        Assert.Equal(ModuleKind.Script, kind);
    }

    [Fact]
    public void Classify_Psd1PointingAtScript_ReturnsScript()
    {
        var manifestPath = WriteFile("MyModule.psd1", "RootModule = 'MyModule.psm1'" + Environment.NewLine);
        var entryPointPath = WriteFile("MyModule.psm1", "# script body");

        var kind = ModuleTypeClassifier.Classify(manifestPath, entryPointPath);

        Assert.Equal(ModuleKind.Script, kind);
    }

    [Fact]
    public void Classify_Psd1PointingAtDll_ReturnsBinary()
    {
        var manifestPath = WriteFile("MyModule.psd1", "RootModule = 'MyModule.dll'" + Environment.NewLine);
        var entryPointPath = WriteFile("MyModule.dll", "fake-dll");

        var kind = ModuleTypeClassifier.Classify(manifestPath, entryPointPath);

        Assert.Equal(ModuleKind.Binary, kind);
    }

    private string WriteFile(string fileName, string contents)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, contents);
        return path;
    }
}

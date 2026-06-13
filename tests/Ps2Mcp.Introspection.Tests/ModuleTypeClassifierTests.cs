using Ps2Mcp.Introspection;

namespace Ps2Mcp.Introspection.Tests;

public sealed class ModuleTypeClassifierTests
{
    [Fact]
    public void Classify_Psm1Input_ReturnsScript()
    {
        var modulePath = "/mods/MyModule.psm1";

        var kind = ModuleTypeClassifier.Classify(modulePath, modulePath);

        Assert.Equal(ModuleKind.Script, kind);
    }

    [Fact]
    public void Classify_Psd1PointingAtScript_ReturnsScript()
    {
        var manifestPath = "/mods/MyModule.psd1";
        var entryPointPath = "/mods/MyModule.psm1";

        var kind = ModuleTypeClassifier.Classify(manifestPath, entryPointPath);

        Assert.Equal(ModuleKind.Script, kind);
    }

    [Fact]
    public void Classify_Psd1PointingAtDll_ReturnsBinary()
    {
        var manifestPath = "/mods/MyModule.psd1";
        var entryPointPath = "/mods/MyModule.dll";

        var kind = ModuleTypeClassifier.Classify(manifestPath, entryPointPath);

        Assert.Equal(ModuleKind.Binary, kind);
    }

    [Fact]
    public void Classify_NullManifestPath_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => ModuleTypeClassifier.Classify(null!, "/mods/MyModule.psm1"));

        Assert.Equal("manifestPath", exception.ParamName);
    }

    [Fact]
    public void Classify_WhitespaceEntryPointPath_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => ModuleTypeClassifier.Classify("/mods/MyModule.psd1", " "));

        Assert.Equal("entryPointPath", exception.ParamName);
    }
}

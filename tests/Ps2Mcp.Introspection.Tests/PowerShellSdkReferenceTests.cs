using System.Management.Automation.Language;
using Ps2Mcp.Introspection;

namespace Ps2Mcp.Introspection.Tests;

public sealed class PowerShellSdkReferenceTests
{
    [Fact]
    public void ParserType_IsReachableFromIntrospection()
    {
        // A direct type reference to the PowerShell AST parser. If the <PackageReference> is missing,
        // this file fails to compile, which is the strongest possible guard against drift.
        var type = typeof(Parser);

        Assert.Equal("System.Management.Automation.Language.Parser", type.FullName);
        Assert.Equal("System.Management.Automation", type.Assembly.GetName().Name);
    }

    [Fact]
    public void Parser_ParseInput_ProducesScriptBlockAstForValidScript()
    {
        // Smoke: AST-only parsing must work without engine hosting. The returned AST is a parse tree;
        // no PowerShell runspace is instantiated, which is the AOT-safe surface §8 mandates.
        var ast = Parser.ParseInput("function Get-Foo { 'hello' }", out _, out _);

        var scriptAst = Assert.IsType<ScriptBlockAst>(ast);
        var functions = scriptAst.FindAll(a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true).ToList();
        Assert.Single(functions);
    }
}

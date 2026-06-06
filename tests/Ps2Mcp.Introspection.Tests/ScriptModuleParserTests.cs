using System.Management.Automation.Language;
using Ps2Mcp.Introspection;

namespace Ps2Mcp.Introspection.Tests;

public sealed class ScriptModuleParserTests : IDisposable
{
    private readonly string _tempDir;

    public ScriptModuleParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ps2mcp-parser-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Parse_ThrowsArgumentNullException_ForNullPath()
    {
        // ArgumentException.ThrowIfNullOrEmpty throws ArgumentNullException for null input.
        Assert.Throws<ArgumentNullException>(() => ScriptModuleParser.Parse(null!));
    }

    [Fact]
    public void Parse_ThrowsArgumentException_ForEmptyPath()
    {
        // ArgumentException.ThrowIfNullOrEmpty throws ArgumentException for empty-string input.
        Assert.Throws<ArgumentException>(() => ScriptModuleParser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_ThrowsFileNotFoundException_ForMissingFile()
    {
        var missing = Path.Combine(_tempDir, "DoesNotExist.psm1");

        Assert.Throws<FileNotFoundException>(() => ScriptModuleParser.Parse(missing));
    }

    [Fact]
    public void Parse_ReturnsAstWithNoErrors_ForEmptyFile()
    {
        var path = WritePsm1(string.Empty);

        var result = ScriptModuleParser.Parse(path);

        Assert.Equal(path, result.FilePath);
        Assert.NotNull(result.Ast);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Parse_DiscoversFunctionDefinition()
    {
        var path = WritePsm1("function Get-Foo { 'hello' }");

        var result = ScriptModuleParser.Parse(path);

        var functions = result.Ast.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true).ToList();
        Assert.Single(functions);
        Assert.Equal("Get-Foo", Assert.IsType<FunctionDefinitionAst>(functions[0]).Name);
    }

    [Fact]
    public void Parse_DiscoversMultipleTopLevelFunctions()
    {
        var path = WritePsm1(
            "function Get-Foo { 'foo' }\n" +
            "function Set-Foo { 'foo' }\n" +
            "function Remove-Foo { 'foo' }\n");

        var result = ScriptModuleParser.Parse(path);

        var names = result.Ast.FindAll(static a => a is FunctionDefinitionAst f, searchNestedScriptBlocks: true)
            .Cast<FunctionDefinitionAst>()
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "Get-Foo", "Remove-Foo", "Set-Foo" }, names);
    }

    [Fact]
    public void Parse_DiscoversNestedFunctions()
    {
        // NestedScriptBlocks=true ensures the inner helper is found alongside the outer one.
        var path = WritePsm1(
            "function Outer { function Inner { 'x' } }");

        var result = ScriptModuleParser.Parse(path);

        var names = result.Ast.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true)
            .Cast<FunctionDefinitionAst>()
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "Inner", "Outer" }, names);
    }

    [Fact]
    public void Parse_SurfacesParamBlockWithTypedMandatoryParameter()
    {
        // Production PowerShell modules use an explicit param() block; that yields the well-known
        // ParamBlockAst/ParameterAst structure that the follow-on extractors (Phase 5 Task 3+) consume.
        var path = WritePsm1(
            "function Get-Foo {\n" +
            "    param(\n" +
            "        [Parameter(Mandatory)]\n" +
            "        [string]\n" +
            "        $Name\n" +
            "    )\n" +
            "}\n");

        var result = ScriptModuleParser.Parse(path);

        var func = Assert.IsType<FunctionDefinitionAst>(
            result.Ast.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true).Single());
        var paramBlock = func.Body.ParamBlock;
        Assert.NotNull(paramBlock);
        var param = paramBlock.Parameters.Single();
        Assert.Equal("Name", param.Name.VariablePath.UserPath);
        var typeConstraint = param.Attributes.OfType<TypeConstraintAst>().Single();
        Assert.Equal("string", typeConstraint.TypeName.Name);
        var parameterAttr = param.Attributes.OfType<AttributeAst>().Single(a => a.TypeName.Name == "Parameter");
        Assert.Contains(parameterAttr.NamedArguments, kv => kv.ArgumentName.Equals("Mandatory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_SurfacesOutputTypeAttribute()
    {
        // [OutputType] is not in func.Body.Attributes when placed above the function body; the parser
        // wraps it in an AttributedExpressionAst inside the body's EndBlock. We locate it via FindAll
        // so the test mirrors what an extractor would do at runtime.
        var path = WritePsm1(
            "function Get-Foo {\n" +
            "    [OutputType('string')]\n" +
            "    'hello'\n" +
            "}\n");

        var result = ScriptModuleParser.Parse(path);

        var func = Assert.IsType<FunctionDefinitionAst>(
            result.Ast.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true).Single());
        var outputType = func.FindAll(a => a is AttributeAst att && att.TypeName.Name == "OutputType", searchNestedScriptBlocks: true)
            .Cast<AttributeAst>()
            .Single();
        var arg = Assert.IsType<StringConstantExpressionAst>(outputType.PositionalArguments[0]);
        Assert.Equal("string", arg.Value);
    }

    [Fact]
    public void Parse_SurfacesCommentBasedHelp()
    {
        var path = WritePsm1(
            "function Get-Foo {\n" +
            "    <#\n" +
            "        .SYNOPSIS\n" +
            "        Gets a foo.\n" +
            "        .DESCRIPTION\n" +
            "        Returns a foo object.\n" +
            "        .EXAMPLE\n" +
            "        Get-Foo\n" +
            "    #>\n" +
            "    'hello'\n" +
            "}\n");

        var result = ScriptModuleParser.Parse(path);

        var func = Assert.IsType<FunctionDefinitionAst>(
            result.Ast.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true).Single());
        var help = func.GetHelpContent();
        Assert.NotNull(help);
        // PowerShell's CommentHelpInfo preserves the line break after the synopsis line; we trim for
        // content-level comparison so this test is robust to whitespace-formatting changes upstream.
        Assert.Equal("Gets a foo.", help!.Synopsis.TrimEnd());
        Assert.Equal("Returns a foo object.", help.Description.TrimEnd());
        Assert.Single(help.Examples);
        Assert.Contains("Get-Foo", help.Examples[0]);
    }

    [Fact]
    public void Parse_CapturesSyntaxErrors_AsValue()
    {
        // Missing function name is a syntax error; the parser must surface it via result.Errors,
        // not throw, so the orchestrator can decide policy.
        var path = WritePsm1("function { 'hello' }");

        var result = ScriptModuleParser.Parse(path);

        Assert.True(result.HasErrors);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_ReturnsPartialAst_EvenWhenSyntaxErrors()
    {
        // Parser.ParseFile returns a (possibly partial) AST even on syntax errors. The parser
        // must hand that AST back to callers so they can extract what they can.
        var path = WritePsm1("function { 'hello' }");

        var result = ScriptModuleParser.Parse(path);

        Assert.NotNull(result.Ast);
    }

    [Fact]
    public void Parse_HasErrorsIsFalse_WhenSourceIsValid()
    {
        var path = WritePsm1("function Get-Foo { 'hello' }");

        var result = ScriptModuleParser.Parse(path);

        Assert.False(result.HasErrors);
        Assert.True(result.Errors.IsDefaultOrEmpty);
    }

    private string WritePsm1(string content)
    {
        var path = Path.Combine(_tempDir, "TestModule.psm1");
        File.WriteAllText(path, content);
        return path;
    }
}

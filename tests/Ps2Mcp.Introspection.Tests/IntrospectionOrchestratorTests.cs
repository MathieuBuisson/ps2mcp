using System;
using System.Collections.Immutable;
using System.Management.Automation.Language;
using Ps2Mcp.Core;
using Xunit;

namespace Ps2Mcp.Introspection.Tests;

public class IntrospectionOrchestratorTests
{
    [Fact]
    public void Introspect_NullModule_ThrowsArgumentNullException()
    {
        var parser = new StubScriptModuleParser(CreateParseResult(
            "C:/modules/EntryPoint.psm1"));
        var scriptIntrospector = new StubScriptModuleIntrospector(CreateServerDefinition("EntryPoint", "Get-Foo"));
        var binaryIntrospector = new StubBinaryModuleIntrospector(CreateServerDefinition("IgnoredBinary", "Get-Bar"));
        var orchestrator = new IntrospectionOrchestrator(parser, scriptIntrospector, binaryIntrospector);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            orchestrator.Introspect(module: null!, runner: null));

        Assert.Equal("module", exception.ParamName);
        Assert.Null(parser.LastFilePath);
        Assert.Null(binaryIntrospector.LastRequest);
    }

    [Fact]
    public void Introspect_ScriptModule_AllowsNullRunner_AndOverridesModuleName()
    {
        const string expectedVersion = "1.2.3";

        var parseResult = CreateParseResult(
            "C:/modules/EntryPoint.psm1");
        var parser = new StubScriptModuleParser(parseResult);
        var scriptIntrospector = new StubScriptModuleIntrospector(CreateServerDefinition("EntryPoint", "Get-Foo", expectedVersion));
        var binaryIntrospector = new StubBinaryModuleIntrospector(CreateServerDefinition("IgnoredBinary", "Get-Bar"));
        var orchestrator = new IntrospectionOrchestrator(parser, scriptIntrospector, binaryIntrospector);
        var module = new ResolvedModule(
            "C:/modules/MyModule.psd1",
            "C:/modules/EntryPoint.psm1",
            "MyModule",
            ModuleKind.Script);

        var result = orchestrator.Introspect(module, runner: null);

        Assert.Equal(module.EntryPointPath, parser.LastFilePath);
        Assert.Same(parseResult, scriptIntrospector.LastParseResult);
        Assert.Null(binaryIntrospector.LastRequest);
        Assert.Equal("MyModule", result.Module.Name);
        Assert.Equal(expectedVersion, result.Module.Version);
        Assert.Single(result.Tools);
        Assert.Equal("Get-Foo", result.Tools[0].ToolName);
    }

    [Fact]
    public void Introspect_BinaryModule_UsesBinaryIntrospector_AndOverridesModuleName()
    {
        var parser = new StubScriptModuleParser(CreateParseResult(
            "C:/modules/Unused.psm1"));
        var scriptIntrospector = new StubScriptModuleIntrospector(CreateServerDefinition("IgnoredScript", "Get-Unused"));
        var binaryIntrospector = new StubBinaryModuleIntrospector(CreateServerDefinition("EntryAssembly", "Get-Bar"));
        var orchestrator = new IntrospectionOrchestrator(parser, scriptIntrospector, binaryIntrospector);
        var module = new ResolvedModule(
            "C:/modules/MyBinary.psd1",
            "C:/modules/EntryAssembly.dll",
            "MyBinary",
            ModuleKind.Binary);
        var runner = new FakePwshRunner();

        var result = orchestrator.Introspect(module, runner);

        Assert.Null(parser.LastFilePath);
        Assert.Null(scriptIntrospector.LastParseResult);
        Assert.NotNull(binaryIntrospector.LastRequest);
        Assert.Equal(module.EntryPointPath, binaryIntrospector.LastRequest!.ModulePath);
        Assert.Same(runner, binaryIntrospector.LastRunner);
        Assert.Equal("MyBinary", result.Module.Name);
        Assert.Null(result.Module.Version);
        Assert.Single(result.Tools);
        Assert.Equal("Get-Bar", result.Tools[0].ToolName);
    }

    [Fact]
    public void Introspect_NullRunnerForBinaryModule_ThrowsArgumentNullException()
    {
        var parser = new StubScriptModuleParser(CreateParseResult(
            "C:/modules/Unused.psm1"));
        var scriptIntrospector = new StubScriptModuleIntrospector(CreateServerDefinition("IgnoredScript", "Get-Unused"));
        var binaryIntrospector = new StubBinaryModuleIntrospector(CreateServerDefinition("EntryAssembly", "Get-Bar"));
        var orchestrator = new IntrospectionOrchestrator(parser, scriptIntrospector, binaryIntrospector);
        var module = new ResolvedModule(
            "C:/modules/MyBinary.psd1",
            "C:/modules/EntryAssembly.dll",
            "MyBinary",
            ModuleKind.Binary);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            orchestrator.Introspect(module, runner: null));

        Assert.Equal("runner", exception.ParamName);
        Assert.Null(binaryIntrospector.LastRequest);
    }

    [Fact]
    public void Introspect_UnsupportedModuleKind_ThrowsInvalidOperationException()
    {
        var parser = new StubScriptModuleParser(CreateParseResult(
            "C:/modules/Unused.psm1"));
        var scriptIntrospector = new StubScriptModuleIntrospector(CreateServerDefinition("IgnoredScript", "Get-Unused"));
        var binaryIntrospector = new StubBinaryModuleIntrospector(CreateServerDefinition("EntryAssembly", "Get-Bar"));
        var orchestrator = new IntrospectionOrchestrator(parser, scriptIntrospector, binaryIntrospector);
        var module = new ResolvedModule(
            "C:/modules/Unknown.psd1",
            "C:/modules/Unknown.psm1",
            "Unknown",
            (ModuleKind)999);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            orchestrator.Introspect(module, runner: null));

        Assert.Contains("Unsupported module kind", exception.Message, StringComparison.Ordinal);
        Assert.Null(parser.LastFilePath);
        Assert.Null(scriptIntrospector.LastParseResult);
        Assert.Null(binaryIntrospector.LastRequest);
    }

    [Fact]
    public void Introspect_ScriptModuleWithParseErrors_ThrowsScriptModuleIntrospectionException()
    {
        var parseResult = CreateParseResult(
            "C:/modules/Broken.psm1",
            "Missing function name.",
            "Unexpected token '}' in expression or statement.");
        var parser = new StubScriptModuleParser(parseResult);
        var scriptIntrospector = new StubScriptModuleIntrospector(CreateServerDefinition("IgnoredScript", "Get-Unused"));
        var binaryIntrospector = new StubBinaryModuleIntrospector(CreateServerDefinition("IgnoredBinary", "Get-Bar"));
        var orchestrator = new IntrospectionOrchestrator(parser, scriptIntrospector, binaryIntrospector);
        var module = new ResolvedModule(
            "C:/modules/Broken.psm1",
            "C:/modules/Broken.psm1",
            "Broken",
            ModuleKind.Script);

        var exception = Assert.Throws<ScriptModuleIntrospectionException>(() =>
            orchestrator.Introspect(module, runner: null));

        Assert.True(parseResult.Errors.Length > 1);
        Assert.Equal(module.EntryPointPath, exception.ModulePath);
        Assert.Equal(parseResult.Errors.Length, exception.ParseErrors.Length);
        Assert.Contains(module.EntryPointPath, exception.Message, StringComparison.Ordinal);
        Assert.Contains($"{parseResult.Errors.Length} error(s)", exception.Message, StringComparison.Ordinal);
        Assert.Contains(parseResult.Errors[0].Message, exception.Message, StringComparison.Ordinal);
        Assert.Contains(parseResult.Errors[1].Message, exception.Message, StringComparison.Ordinal);
        Assert.Equal(parseResult.Errors[0].Message, exception.ParseErrors[0]);
        Assert.Equal(parseResult.Errors[1].Message, exception.ParseErrors[1]);
        Assert.Null(scriptIntrospector.LastParseResult);
        Assert.Null(binaryIntrospector.LastRequest);
    }

    private static ScriptModuleParseResult CreateParseResult(string filePath, params string[] parseErrorMessages)
    {
        if (parseErrorMessages.Length == 0)
        {
            return new ScriptModuleParseResult(
                filePath,
                Ast: null!,
                Errors: ImmutableArray<ParseError>.Empty);
        }

        var errorBuilder = ImmutableArray.CreateBuilder<ParseError>(parseErrorMessages.Length);
        for (var index = 0; index < parseErrorMessages.Length; index++)
        {
            errorBuilder.Add(CreateParseError(filePath, parseErrorMessages[index], index + 1));
        }

        return new ScriptModuleParseResult(
            filePath,
            Ast: null!,
            Errors: errorBuilder.MoveToImmutable());
    }

    private static ParseError CreateParseError(string filePath, string message, int lineNumber)
    {
        const string lineText = "test";

        var start = new ScriptPosition(filePath, lineNumber, 1, lineText);
        var end = new ScriptPosition(filePath, lineNumber, lineText.Length + 1, lineText);
        var extent = new ScriptExtent(start, end);

        return new ParseError(extent, $"TestParseError{lineNumber}", message);
    }

    private static McpServerDefinition CreateServerDefinition(string moduleName, string toolName, string? version = null)
    {
        return new McpServerDefinition(
            new ModuleDefinition(moduleName, version),
            ImmutableArray.Create(
                new ToolDefinition(
                    ToolName: toolName,
                    SourceCommand: toolName,
                    Description: string.Empty,
                    Parameters: ImmutableArray<ParameterDefinition>.Empty,
                    RequiredParameterSet: null,
                    Schema: new SchemaDefinition(
                        Type: "object",
                        Properties: ImmutableArray<SchemaProperty>.Empty,
                        Required: ImmutableArray<string>.Empty,
                        Items: null),
                    Execution: new ExecutionDefinition(SerializationDepth: 4),
                    Help: null,
                    Output: null)));
    }

    private sealed class StubScriptModuleParser : IScriptModuleParser
    {
        private readonly ScriptModuleParseResult _parseResult;

        public StubScriptModuleParser(ScriptModuleParseResult parseResult)
        {
            _parseResult = parseResult;
        }

        public string? LastFilePath { get; private set; }

        public ScriptModuleParseResult Parse(string filePath)
        {
            LastFilePath = filePath;
            return _parseResult;
        }
    }

    private sealed class StubScriptModuleIntrospector : IScriptModuleIntrospector
    {
        private readonly McpServerDefinition _definition;

        public StubScriptModuleIntrospector(McpServerDefinition definition)
        {
            _definition = definition;
        }

        public ScriptModuleParseResult? LastParseResult { get; private set; }

        public McpServerDefinition Introspect(ScriptModuleParseResult parseResult)
        {
            LastParseResult = parseResult;
            return _definition;
        }
    }

    private sealed class StubBinaryModuleIntrospector : IBinaryModuleIntrospector
    {
        private readonly McpServerDefinition _definition;

        public StubBinaryModuleIntrospector(McpServerDefinition definition)
        {
            _definition = definition;
        }

        public BinaryModuleIntrospectionRequest? LastRequest { get; private set; }

        public IPwshRunner? LastRunner { get; private set; }

        public McpServerDefinition Introspect(BinaryModuleIntrospectionRequest request, IPwshRunner runner)
        {
            LastRequest = request;
            LastRunner = runner;
            return _definition;
        }
    }
}

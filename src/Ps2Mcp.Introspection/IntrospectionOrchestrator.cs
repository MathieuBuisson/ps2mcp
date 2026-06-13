using System;
using System.Linq;
using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Introspection;

public sealed class IntrospectionOrchestrator
{
    private readonly IScriptModuleParser _scriptModuleParser;
    private readonly IScriptModuleIntrospector _scriptModuleIntrospector;
    private readonly IBinaryModuleIntrospector _binaryModuleIntrospector;

    public IntrospectionOrchestrator()
        : this(
            new ScriptModuleParserAdapter(),
            new ScriptModuleIntrospectorAdapter(),
            new BinaryModuleIntrospectorAdapter())
    {
    }

    internal IntrospectionOrchestrator(
        IScriptModuleParser scriptModuleParser,
        IScriptModuleIntrospector scriptModuleIntrospector,
        IBinaryModuleIntrospector binaryModuleIntrospector)
    {
        _scriptModuleParser = scriptModuleParser ?? throw new ArgumentNullException(nameof(scriptModuleParser));
        _scriptModuleIntrospector = scriptModuleIntrospector ?? throw new ArgumentNullException(nameof(scriptModuleIntrospector));
        _binaryModuleIntrospector = binaryModuleIntrospector ?? throw new ArgumentNullException(nameof(binaryModuleIntrospector));
    }

    public McpServerDefinition Introspect(ResolvedModule module, IPwshRunner? runner)
    {
        ArgumentNullException.ThrowIfNull(module);

        var definition = module.Kind switch
        {
            ModuleKind.Script => IntrospectScriptModule(module),
            ModuleKind.Binary => _binaryModuleIntrospector.Introspect(
                new BinaryModuleIntrospectionRequest(module.EntryPointPath),
                runner ?? throw new ArgumentNullException(nameof(runner))),
            _ => throw new InvalidOperationException($"Unsupported module kind '{module.Kind}'.")
        };

        return definition with
        {
            Module = new ModuleDefinition(module.ModuleName, definition.Module.Version)
        };
    }

    private McpServerDefinition IntrospectScriptModule(ResolvedModule module)
    {
        var parseResult = _scriptModuleParser.Parse(module.EntryPointPath);
        if (parseResult.HasErrors)
        {
            var parseErrors = parseResult.Errors
                .Select(static error => error.Message)
                .ToImmutableArray();

            throw new ScriptModuleIntrospectionException(module.EntryPointPath, parseErrors);
        }

        return _scriptModuleIntrospector.Introspect(parseResult);
    }
}

internal interface IScriptModuleParser
{
    ScriptModuleParseResult Parse(string filePath);
}

internal interface IScriptModuleIntrospector
{
    McpServerDefinition Introspect(ScriptModuleParseResult parseResult);
}

internal interface IBinaryModuleIntrospector
{
    McpServerDefinition Introspect(BinaryModuleIntrospectionRequest request, IPwshRunner runner);
}

internal sealed class ScriptModuleParserAdapter : IScriptModuleParser
{
    public ScriptModuleParseResult Parse(string filePath) => ScriptModuleParser.Parse(filePath);
}

internal sealed class ScriptModuleIntrospectorAdapter : IScriptModuleIntrospector
{
    public McpServerDefinition Introspect(ScriptModuleParseResult parseResult) =>
        ScriptModuleIntrospector.Introspect(parseResult);
}

internal sealed class BinaryModuleIntrospectorAdapter : IBinaryModuleIntrospector
{
    public McpServerDefinition Introspect(BinaryModuleIntrospectionRequest request, IPwshRunner runner) =>
        BinaryModuleIntrospector.Introspect(request, runner);
}

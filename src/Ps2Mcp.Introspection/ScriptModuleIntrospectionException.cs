using System;
using System.Collections.Immutable;

namespace Ps2Mcp.Introspection;

public sealed class ScriptModuleIntrospectionException : Exception
{
    public ScriptModuleIntrospectionException(
        string modulePath,
        ImmutableArray<string> parseErrors)
        : base(BuildMessage(modulePath, parseErrors))
    {
        ArgumentNullException.ThrowIfNull(modulePath);

        ModulePath = modulePath;
        ParseErrors = parseErrors;
    }

    public string ModulePath { get; }
    public ImmutableArray<string> ParseErrors { get; }

    private static string BuildMessage(string modulePath, ImmutableArray<string> parseErrors)
    {
        var errors = parseErrors.IsDefaultOrEmpty
            ? ImmutableArray.Create("no parser errors were reported")
            : parseErrors;
        var errorMessages = string.Join(Environment.NewLine, errors);

        return $"Script module '{modulePath}' could not be parsed ({errors.Length} error(s)):{Environment.NewLine}{errorMessages}";
    }
}

namespace Ps2Mcp.Cli;

internal enum CliCommand
{
    Build,
    Verify,
}

internal enum GenerationTarget
{
    TypeScript,
    Python,
}

internal sealed record CliInvocation(
    CliCommand Command,
    string ModulePath,
    GenerationTarget Target,
    string OutputDirectory);

internal enum CliParseResultKind
{
    Invocation,
    Help,
    Version,
}

internal sealed record CliParseResult(
    CliParseResultKind Kind,
    CliInvocation? Invocation)
{
    internal static CliParseResult Help { get; } = new(CliParseResultKind.Help, null);

    internal static CliParseResult Version { get; } = new(CliParseResultKind.Version, null);

    internal static CliParseResult ForInvocation(CliInvocation invocation) => new(CliParseResultKind.Invocation, invocation);
}

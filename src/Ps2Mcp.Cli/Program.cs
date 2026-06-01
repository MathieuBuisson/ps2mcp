using System;
using System.IO;

namespace Ps2Mcp.Cli;

internal static class Program
{
    private static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    internal static int Run(string[] args, TextWriter standardOutput, TextWriter standardError)
    {
        if (!CliArgumentsParser.TryParse(args, out var parseResult, out var errorMessage))
        {
            standardError.WriteLine(errorMessage);
            standardError.WriteLine(CliArgumentsParser.UsageText);
            return ExitCodeDispatcher.Dispatch(CliOutcome.Fatal);
        }

        return parseResult.Kind switch
        {
            CliParseResultKind.Help => WriteSuccess(standardOutput, CliArgumentsParser.UsageText),
            CliParseResultKind.Version => WriteSuccess(standardOutput, CliVersionProvider.DisplayVersion),
            CliParseResultKind.Invocation when parseResult.Invocation is not null => ExitCodeDispatcher.Dispatch(CliOutcome.Success),
            _ => throw new InvalidOperationException("CLI parse result is invalid."),
        };
    }

    private static int WriteSuccess(TextWriter writer, string value)
    {
        writer.WriteLine(value);
        return ExitCodeDispatcher.Dispatch(CliOutcome.Success);
    }
}

using System;
using System.IO;
using Ps2Mcp.Introspection;

namespace Ps2Mcp.Cli;

internal static class Program
{
    private static int Main(string[] args) => Run(args, Console.Out, Console.Error, new PwshRunner());

    internal static int Run(string[] args, TextWriter standardOutput, TextWriter standardError, IPwshRunner pwshRunner)
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
            CliParseResultKind.Invocation when parseResult.Invocation is not null => RunPreflight(pwshRunner, standardError),
            _ => throw new InvalidOperationException("CLI parse result is invalid."),
        };
    }

    private static int RunPreflight(IPwshRunner pwshRunner, TextWriter standardError)
    {
        var probeResult = PwshProbe.Probe(pwshRunner);
        if (probeResult.Status != PwshProbeStatus.Ok)
        {
            standardError.WriteLine(probeResult.DiagnosticMessage);
            return ExitCodeDispatcher.Dispatch(CliOutcome.Fatal);
        }

        return ExitCodeDispatcher.Dispatch(CliOutcome.Success);
    }

    private static int WriteSuccess(TextWriter writer, string value)
    {
        writer.WriteLine(value);
        return ExitCodeDispatcher.Dispatch(CliOutcome.Success);
    }
}

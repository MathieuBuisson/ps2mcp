using System;

namespace Ps2Mcp.Cli;

internal enum CliOutcome
{
    Success,
    Fatal,
    Drift,
}

internal static class ExitCodeDispatcher
{
    internal static int Dispatch(CliOutcome outcome) => outcome switch
    {
        CliOutcome.Success => ExitCodes.Success,
        CliOutcome.Fatal => ExitCodes.Fatal,
        CliOutcome.Drift => ExitCodes.Drift,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown CLI outcome."),
    };
}

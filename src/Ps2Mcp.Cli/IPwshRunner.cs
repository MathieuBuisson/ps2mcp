using System;

namespace Ps2Mcp.Cli;

internal interface IPwshRunner
{
    PwshInvocationResult Invoke(PwshInvocation invocation);
}

internal enum PwshStartFailureKind
{
    NotFound,
    Failed,
}

internal sealed class PwshStartException : Exception
{
    public PwshStartException(PwshStartFailureKind kind, string executable, Exception innerException)
        : base(BuildMessage(kind, executable, innerException), innerException)
    {
        Kind = kind;
    }

    public PwshStartFailureKind Kind { get; }

    private static string BuildMessage(PwshStartFailureKind kind, string executable, Exception innerException) => kind switch
    {
        PwshStartFailureKind.NotFound => $"The '{executable}' executable was not found on PATH.",
        _ => $"The '{executable}' executable could not be started: {innerException.Message}",
    };
}

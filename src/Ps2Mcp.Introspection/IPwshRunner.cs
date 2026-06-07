using System;

namespace Ps2Mcp.Introspection;

// The pwsh-running surface is consumed by both Ps2Mcp.Cli (PwshProbe, Program) and
// Ps2Mcp.Introspection (BinaryModuleIntrospector). Exposing these types as public keeps
// the friend-assembly / ProduceReferenceAssembly configuration out of the way; the surface
// is internal to the compiler's implementation and is not intended as a stable API.

public interface IPwshRunner
{
    PwshInvocationResult Invoke(PwshInvocation invocation);
}

public enum PwshStartFailureKind
{
    NotFound,
    Failed,
}

public sealed class PwshStartException : Exception
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

// Thrown when a pwsh invocation exceeds its declared timeout. The process has been
// killed by the time this exception is raised; the partial stdout/stderr captured up
// to the kill point are exposed on the exception so callers can log diagnostic context
// (e.g. what the module was emitting when it hung).
public sealed class PwshTimeoutException : Exception
{
    public PwshTimeoutException(
        TimeSpan timeout,
        string partialStandardOutput,
        string partialStandardError,
        string executable)
        : base(BuildMessage(timeout, executable))
    {
        Timeout = timeout;
        PartialStandardOutput = partialStandardOutput;
        PartialStandardError = partialStandardError;
        Executable = executable;
    }

    public TimeSpan Timeout { get; }
    public string PartialStandardOutput { get; }
    public string PartialStandardError { get; }
    public string Executable { get; }

    private static string BuildMessage(TimeSpan timeout, string executable) =>
        $"The '{executable}' process did not exit within the {timeout.TotalSeconds:0.##}-second timeout and was killed.";
}

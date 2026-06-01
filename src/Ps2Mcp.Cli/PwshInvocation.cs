using System.Collections.Generic;

namespace Ps2Mcp.Cli;

internal sealed record PwshInvocation(
    string Executable,
    IReadOnlyList<string> Arguments);

internal sealed record PwshInvocationResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

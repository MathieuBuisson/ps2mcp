using System;
using System.Collections.Generic;

namespace Ps2Mcp.Cli;

// Timeout is optional: a null value (the default) means the runner waits indefinitely
// for the process to exit. Callers should set this for any long-running invocation
// (e.g. binary-module introspection) where a hung `pwsh` would otherwise block the CLI.
internal sealed record PwshInvocation(
    string Executable,
    IReadOnlyList<string> Arguments,
    TimeSpan? Timeout = null);

internal sealed record PwshInvocationResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

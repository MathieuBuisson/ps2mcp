using System;
using System.Collections.Generic;
using Ps2Mcp.Introspection;

namespace Ps2Mcp.Cli;

internal enum PwshProbeStatus
{
    Ok,
    NotFound,
    Failed,
    InvalidOutput,
    UnsupportedVersion,
}

internal sealed record PwshProbeResult(
    PwshProbeStatus Status,
    int? MajorVersion,
    string? DiagnosticMessage);

internal static class PwshProbe
{
    private const string PwshExecutableName = "pwsh";
    private const int MinimumSupportedMajorVersion = 7;

    private static readonly IReadOnlyList<string> ProbeArguments = new[]
    {
        "-NoProfile",
        "-NonInteractive",
        "-Command",
        "$PSVersionTable.PSVersion.Major",
    };

    internal static PwshProbeResult Probe(IPwshRunner runner)
    {
        var invocation = new PwshInvocation(PwshExecutableName, ProbeArguments);

        PwshInvocationResult result;
        try
        {
            result = runner.Invoke(invocation);
        }
        catch (PwshStartException ex) when (ex.Kind == PwshStartFailureKind.NotFound)
        {
            return new PwshProbeResult(
                PwshProbeStatus.NotFound,
                null,
                "`pwsh` 7.x is required but the executable was not found on PATH.");
        }
        catch (PwshStartException ex)
        {
            var innerMessage = ex.InnerException?.Message;
            return new PwshProbeResult(
                PwshProbeStatus.Failed,
                null,
                innerMessage is null
                    ? "`pwsh` could not be started."
                    : $"`pwsh` could not be started: {innerMessage}");
        }

        if (result.ExitCode != 0)
        {
            return new PwshProbeResult(
                PwshProbeStatus.Failed,
                null,
                $"`pwsh` failed with exit code {result.ExitCode}: {result.StandardError}");
        }

        var trimmedOutput = result.StandardOutput.Trim();
        if (!int.TryParse(trimmedOutput, out var major))
        {
            return new PwshProbeResult(
                PwshProbeStatus.InvalidOutput,
                null,
                $"`pwsh` did not return a parseable version. Output: {trimmedOutput}");
        }

        if (major < MinimumSupportedMajorVersion)
        {
            return new PwshProbeResult(
                PwshProbeStatus.UnsupportedVersion,
                major,
                $"`pwsh` 7.x is required; found version {major}.");
        }

        return new PwshProbeResult(PwshProbeStatus.Ok, major, null);
    }
}

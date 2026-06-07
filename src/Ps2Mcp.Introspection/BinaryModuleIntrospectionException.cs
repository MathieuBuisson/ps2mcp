using System;

namespace Ps2Mcp.Introspection;

// Thrown when a binary module cannot be introspected. Carries the module path, the
// non-zero pwsh exit code, and the captured stderr so the CLI / orchestrator can
// surface a structured error to the user. The Classifier property is a hint set by
// later phases (Task 5: Windows-only-DLL detection) to distinguish "file not found"
// from "platform mismatch" from generic import failure; it stays null when no
// classification is available.
public sealed class BinaryModuleIntrospectionException : Exception
{
    public BinaryModuleIntrospectionException(
        string modulePath,
        int exitCode,
        string standardError,
        string? classifier = null)
        : base(BuildMessage(modulePath, exitCode, standardError, classifier))
    {
        ModulePath = modulePath;
        ExitCode = exitCode;
        StandardError = standardError;
        Classifier = classifier;
    }

    public string ModulePath { get; }
    public int ExitCode { get; }
    public string StandardError { get; }
    public string? Classifier { get; }

    private static string BuildMessage(string modulePath, int exitCode, string standardError, string? classifier)
    {
        var detail = string.IsNullOrWhiteSpace(standardError)
            ? "no error output"
            : standardError.Trim();
        return classifier is null
            ? $"Binary module '{modulePath}' introspection failed with exit code {exitCode}: {detail}"
            : $"Binary module '{modulePath}' introspection failed [{classifier}] with exit code {exitCode}: {detail}";
    }
}

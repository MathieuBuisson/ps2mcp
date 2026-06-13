using System;

namespace Ps2Mcp.Introspection;

// Thrown when a binary module cannot be introspected. Carries the module path, the
// non-zero pwsh exit code, and the captured stderr so the CLI / orchestrator can
// surface a structured error to the user. The Classifier property is a structured
// hint identifying the failure category (for example MissingAssembly,
// PlatformMismatch, or ImportFailure); it stays null when no classification is
// available.
public sealed class BinaryModuleIntrospectionException : Exception
{
    public BinaryModuleIntrospectionException(
        string modulePath,
        int exitCode,
        string standardError,
        string? classifier = null)
        : this(modulePath, exitCode, standardError, classifier, innerException: null)
    {
    }

    public BinaryModuleIntrospectionException(
        string modulePath,
        int exitCode,
        string standardError,
        string? classifier,
        Exception? innerException)
        : base(BuildMessage(modulePath, exitCode, standardError, classifier), innerException)
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
        var prefix = classifier is null
            ? $"Binary module '{modulePath}' introspection failed with exit code {exitCode}: {detail}"
            : $"Binary module '{modulePath}' introspection failed [{classifier}] with exit code {exitCode}: {detail}";
        var guidance = classifier switch
        {
            BinaryModuleClassifiers.MissingAssembly => " Ensure the module and its dependent assemblies are present and import successfully under pwsh 7.x on this machine.",
            BinaryModuleClassifiers.PlatformMismatch => " The module appears to depend on Windows-only or otherwise incompatible binaries. Run the compiler on a host OS where the module imports successfully under pwsh 7.x.",
            BinaryModuleClassifiers.ImportFailure => " Fix the module import error and retry once the module loads successfully under pwsh 7.x.",
            _ => string.Empty,
        };

        return prefix + guidance;
    }
}

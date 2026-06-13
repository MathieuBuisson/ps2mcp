using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ps2Mcp.Core;

namespace Ps2Mcp.Introspection;

// Input contract for BinaryModuleIntrospector.Introspect.
public sealed record BinaryModuleIntrospectionRequest(
    string ModulePath,
    TimeSpan? Timeout = null,
    string? PwshExecutable = null);

// Drives the binary-module introspection pipeline: load Introspection.ps1 from
// the assembly's embedded resources, write it to a temp file, invoke pwsh with
// the script + -ModulePath argument, capture stdout, parse the JSON payload, and
// map it into IR. The temp file is always removed in a finally block, even on
// timeout / parse failure / non-zero exit.
public static class BinaryModuleIntrospector
{
    // Resource name format is "Namespace.Path.With.Dots" by default. The script
    // is embedded as "Ps2Mcp.Introspection.Introspection.ps1".
    private const string ScriptResourceName = "Ps2Mcp.Introspection.Introspection.ps1";

    // Exit code emitted by Introspection.ps1 specifically for Import-Module
    // failures, so the C# layer can distinguish them from other script errors.
    private const int ImportModuleFailureExitCode = 3;

    private static readonly JsonTypeInfo<BinaryIntrospectionPayload> PayloadTypeInfo =
        BinaryIntrospectionJsonSerializerContext.Default.BinaryIntrospectionPayload;

    public static McpServerDefinition Introspect(
        BinaryModuleIntrospectionRequest request,
        IPwshRunner runner)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(runner);

        var scriptPath = WriteScriptToTempFile();
        try
        {
            var arguments = new[]
            {
                "-NoProfile",
                "-NonInteractive",
                "-File",
                scriptPath,
                "-ModulePath",
                request.ModulePath,
            };
            var executable = request.PwshExecutable ?? "pwsh";
            var invocation = new PwshInvocation(executable, arguments, request.Timeout);
            var result = runner.Invoke(invocation);

            if (result.ExitCode != 0)
            {
                throw new BinaryModuleIntrospectionException(
                    request.ModulePath,
                    result.ExitCode,
                    result.StandardError,
                    ClassifyImportFailure(result.ExitCode, result.StandardError));
            }

            return ParsePayload(result.StandardOutput, request.ModulePath);
        }
        finally
        {
            TryDelete(scriptPath);
        }
    }

    // Loads the embedded PowerShell script into a temp file. The temp file's path
    // is what pwsh sees on the -File argument, sidestepping the argument-length
    // limits and quoting pitfalls of inlining the script via -Command. UTF-8 with
    // no BOM is sufficient for PowerShell 7 to read the file correctly, and the
    // temp file is created owner-only on Unix hosts.
    private static string WriteScriptToTempFile()
    {
        var assembly = typeof(BinaryModuleIntrospector).Assembly;
        using var stream = assembly.GetManifestResourceStream(ScriptResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ScriptResourceName}' was not found. " +
                "The Introspection.ps1 file must be added to the .csproj as an <EmbeddedResource>.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = reader.ReadToEnd();

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"ps2mcp-introspect-{Guid.NewGuid():N}.ps1");

        using (var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            fileStream.Write(bytes);
        }

        return tempPath;
    }

    // Normalize an optional trailing line terminator without trimming any other
    // characters from the payload. The script writes JSON with Console.Out.Write,
    // so a newline is not expected, but CR/LF trimming keeps empty-output handling
    // stable if a host or future script change appends one.
    // A parse failure is the script's fault, so it's wrapped in
    // BinaryModuleIntrospectionException to keep the caller's catch block uniform.
    private static McpServerDefinition ParsePayload(string stdout, string modulePath)
    {
        var trimmed = stdout.TrimEnd('\r', '\n');
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new BinaryModuleIntrospectionException(
                modulePath,
                exitCode: 0,
                standardError: "introspection script produced empty output",
                classifier: BinaryModuleClassifiers.EmptyOutput);
        }

        BinaryIntrospectionPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize(trimmed, PayloadTypeInfo);
        }
        catch (JsonException ex)
        {
            throw new BinaryModuleIntrospectionException(
                modulePath,
                exitCode: 0,
                standardError: $"failed to parse introspection JSON: {ex.Message}",
                classifier: BinaryModuleClassifiers.InvalidJson,
                innerException: ex);
        }

        if (payload is null)
        {
            throw new BinaryModuleIntrospectionException(
                modulePath,
                exitCode: 0,
                standardError: "introspection JSON deserialized to null",
                classifier: BinaryModuleClassifiers.InvalidJson);
        }

        return CommandMetadataMapper.Map(payload);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ps2mcp] Failed to delete temp script '{path}': {ex.Message}");
            // Temp files are best-effort cleanup; the OS will reap them eventually.
        }
    }

    private static string? ClassifyImportFailure(int exitCode, string standardError)
    {
        if (exitCode != ImportModuleFailureExitCode)
        {
            return null;
        }

        if (ContainsIgnoreCase(standardError, "incorrect format") ||
            ContainsIgnoreCase(standardError, "badimageformatexception") ||
            ContainsIgnoreCase(standardError, "platformnotsupportedexception") ||
            ContainsIgnoreCase(standardError, "not supported on this platform") ||
            ContainsIgnoreCase(standardError, "unable to load shared library") ||
            ContainsIgnoreCase(standardError, "dllnotfoundexception") ||
            ContainsIgnoreCase(standardError, "is not a valid win32 application"))
        {
            return BinaryModuleClassifiers.PlatformMismatch;
        }

        if (ContainsIgnoreCase(standardError, "could not load file or assembly") ||
            ContainsIgnoreCase(standardError, "filenotfoundexception") ||
            ContainsIgnoreCase(standardError, "the system cannot find the file specified") ||
            ContainsIgnoreCase(standardError, "could not find file"))
        {
            return BinaryModuleClassifiers.MissingAssembly;
        }

        return BinaryModuleClassifiers.ImportFailure;
    }

    private static bool ContainsIgnoreCase(string value, string match) =>
        value.Contains(match, StringComparison.OrdinalIgnoreCase);
}

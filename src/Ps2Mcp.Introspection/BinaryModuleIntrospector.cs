using System;
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
                    result.StandardError);
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
    // no BOM is sufficient for PowerShell 7 to read the file correctly.
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
        File.WriteAllText(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return tempPath;
    }

    // Removes only the trailing newline that ConvertTo-Json emits, without
    // stripping spaces that could legitimately appear in JSON string values.
    // A parse failure is the script's fault, so it's wrapped in
    // BinaryModuleIntrospectionException to keep the caller's catch block uniform.
    private static McpServerDefinition ParsePayload(string stdout, string modulePath)
    {
        var trimmed = stdout.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? stdout[..^Environment.NewLine.Length]
            : stdout;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new BinaryModuleIntrospectionException(
                modulePath,
                exitCode: 0,
                standardError: "introspection script produced empty output",
                classifier: "EmptyOutput");
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
                classifier: "InvalidJson");
        }

        if (payload is null)
        {
            throw new BinaryModuleIntrospectionException(
                modulePath,
                exitCode: 0,
                standardError: "introspection JSON deserialized to null",
                classifier: "InvalidJson");
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
        catch
        {
            // Temp files are best-effort cleanup; the OS will reap them eventually.
        }
    }
}

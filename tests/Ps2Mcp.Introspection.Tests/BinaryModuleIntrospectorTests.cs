using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Ps2Mcp.Core;
using Xunit;

namespace Ps2Mcp.Introspection.Tests;

// Drives BinaryModuleIntrospector via a stub IPwshRunner, asserting:
//   - the embedded script is written to a temp file and pwsh is invoked with
//     the correct arguments (NoProfile, NonInteractive, -File, -ModulePath);
//   - the script's stdout is parsed and mapped to IR;
//   - the temp file is cleaned up after a successful run;
//   - non-zero exit codes raise BinaryModuleIntrospectionException carrying
//     the captured stderr and exit code;
//   - empty / malformed stdout raises BinaryModuleIntrospectionException with
//     a classifier so the CLI can surface a useful message.
// The real pwsh process is never spawned; the production power-shell I/O path
// is exercised end-to-end by the .NET integration tests (see CliProcess-based
// tests) and the unit tests focus on the orchestrator's contract.
public class BinaryModuleIntrospectorTests
{
    [Fact]
    public void Introspect_SuccessfulInvocation_BuildsIrFromCannedStdout()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation => new PwshInvocationResult(0, BuildValidPayload(), string.Empty);

        var result = BinaryModuleIntrospector.Introspect(
            new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
            runner);

        Assert.Equal("MyMod", result.Module.Name);
        Assert.Single(result.Tools);
        Assert.Equal("Get-Foo", result.Tools[0].ToolName);
    }

    [Fact]
    public void Introspect_PassesCorrectArgumentsToPwsh()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation => new PwshInvocationResult(0, BuildValidPayload(), string.Empty);

        BinaryModuleIntrospector.Introspect(
            new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll", PwshExecutable: "C:/pwsh.exe"),
            runner);

        var captured = Assert.Single(runner.StartCalls);
        Assert.Equal("C:/pwsh.exe", captured.Executable);
        Assert.Equal(new[]
        {
            "-NoProfile", "-NonInteractive", "-File", captured.Arguments[3], "-ModulePath", "C:/modules/MyMod.dll",
        }, captured.Arguments);
    }

    [Fact]
    public void Introspect_ForwardsTimeoutToInvocation()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation => new PwshInvocationResult(0, BuildValidPayload(), string.Empty);

        var timeout = TimeSpan.FromSeconds(30);
        BinaryModuleIntrospector.Introspect(
            new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll", Timeout: timeout),
            runner);

        var captured = Assert.Single(runner.StartCalls);
        Assert.Equal(timeout, captured.Timeout);
    }

    [Fact]
    public void Introspect_NonZeroExitCode_WrapsInBinaryModuleIntrospectionException()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation => new PwshInvocationResult(2, string.Empty, "Import-Module: file not found");

        var ex = Assert.Throws<BinaryModuleIntrospectionException>(() =>
            BinaryModuleIntrospector.Introspect(
                new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
                runner));

        Assert.Equal("C:/modules/MyMod.dll", ex.ModulePath);
        Assert.Equal(2, ex.ExitCode);
        Assert.Null(ex.Classifier);
        Assert.Contains("file not found", ex.StandardError);
    }

    [Fact]
    public void Introspect_ImportFailureWithMissingAssembly_ClassifiesAndSurfacesActionableMessage()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation => new PwshInvocationResult(
            3,
            string.Empty,
            "Failed to import module 'C:/modules/MyMod.dll': Could not load file or assembly 'Contoso.Dependency, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'. The system cannot find the file specified.");

        var ex = Assert.Throws<BinaryModuleIntrospectionException>(() =>
            BinaryModuleIntrospector.Introspect(
                new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
                runner));

        Assert.Equal("MissingAssembly", ex.Classifier);
        Assert.Contains("dependent assemblies are present", ex.Message);
    }

    [Fact]
    public void Introspect_ImportFailureWithPlatformMismatch_ClassifiesAndSurfacesActionableMessage()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation => new PwshInvocationResult(
            3,
            string.Empty,
            "Failed to import module 'C:/modules/MyMod.dll': Could not load file or assembly 'Contoso.WindowsOnly'. An attempt was made to load a program with an incorrect format.");

        var ex = Assert.Throws<BinaryModuleIntrospectionException>(() =>
            BinaryModuleIntrospector.Introspect(
                new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
                runner));

        Assert.Equal("PlatformMismatch", ex.Classifier);
        Assert.Contains("Windows-only or otherwise incompatible binaries", ex.Message);
    }

    [Fact]
    public void Introspect_ImportFailureWithoutKnownPattern_ClassifiesAsGenericImportFailure()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation => new PwshInvocationResult(
            3,
            string.Empty,
            "Failed to import module 'C:/modules/MyMod.dll': Module initialization failed with a custom startup error.");

        var ex = Assert.Throws<BinaryModuleIntrospectionException>(() =>
            BinaryModuleIntrospector.Introspect(
                new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
                runner));

        Assert.Equal("ImportFailure", ex.Classifier);
        Assert.Contains("Fix the module import error and retry", ex.Message);
    }

    [Fact]
    public void Introspect_EmptyStdout_WrapsWithEmptyOutputClassifier()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation => new PwshInvocationResult(0, string.Empty, string.Empty);

        var ex = Assert.Throws<BinaryModuleIntrospectionException>(() =>
            BinaryModuleIntrospector.Introspect(
                new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
                runner));

        Assert.Equal("EmptyOutput", ex.Classifier);
        Assert.Equal(0, ex.ExitCode);
    }

    [Fact]
    public void Introspect_MalformedStdout_WrapsWithInvalidJsonClassifier()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation => new PwshInvocationResult(0, "not valid json{", string.Empty);

        var ex = Assert.Throws<BinaryModuleIntrospectionException>(() =>
            BinaryModuleIntrospector.Introspect(
                new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
                runner));

        Assert.Equal("InvalidJson", ex.Classifier);
        Assert.IsType<JsonException>(ex.InnerException);
    }

    [Fact]
    public void Introspect_TempScriptFileIsCleanedUpOnSuccess()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation =>
        {
            var fileIndex = Array.IndexOf(invocation.Arguments.ToArray(), "-File") + 1;
            TempFilePath = invocation.Arguments[fileIndex];
            return new PwshInvocationResult(0, BuildValidPayload(), string.Empty);
        };

        BinaryModuleIntrospector.Introspect(
            new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
            runner);

        Assert.NotNull(TempFilePath);
        Assert.False(File.Exists(TempFilePath),
            "Temp script file should be removed after a successful run.");
    }

    [Fact]
    public void Introspect_TempScriptFileIsCleanedUpOnFailure()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation =>
        {
            var fileIndex = Array.IndexOf(invocation.Arguments.ToArray(), "-File") + 1;
            TempFilePath = invocation.Arguments[fileIndex];
            return new PwshInvocationResult(2, string.Empty, "boom");
        };

        Assert.Throws<BinaryModuleIntrospectionException>(() =>
            BinaryModuleIntrospector.Introspect(
                new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
                runner));

        Assert.NotNull(TempFilePath);
        Assert.False(File.Exists(TempFilePath),
            "Temp script file should be removed even when pwsh exits non-zero.");
    }

    [Fact]
    public void Introspect_TempScriptFileUsesUserOnlyPermissionsOnUnix()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation =>
        {
            var fileIndex = Array.IndexOf(invocation.Arguments.ToArray(), "-File") + 1;
            var scriptPath = invocation.Arguments[fileIndex];

            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(scriptPath));
            }

            return new PwshInvocationResult(0, BuildValidPayload(), string.Empty);
        };

        BinaryModuleIntrospector.Introspect(
            new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
            runner);
    }

    [Fact]
    public void Introspect_EmbeddedScriptContainsExpectedPowerShellCommands()
    {
        var runner = new FakePwshRunner();
        runner.OnInvoke = invocation =>
        {
            var fileIndex = Array.IndexOf(invocation.Arguments.ToArray(), "-File") + 1;
            var scriptPath = invocation.Arguments[fileIndex];
            var scriptContent = File.ReadAllText(scriptPath);
            Assert.Contains("Import-Module", scriptContent);
            Assert.Contains("Get-Command -Module", scriptContent);
            Assert.Contains("ConvertTo-Json", scriptContent);
            return new PwshInvocationResult(0, BuildValidPayload(), string.Empty);
        };

        BinaryModuleIntrospector.Introspect(
            new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
            runner);
    }

    [Fact]
    public void Introspect_NullRequest_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BinaryModuleIntrospector.Introspect(null!, new FakePwshRunner()));
    }

    [Fact]
    public void Introspect_NullRunner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BinaryModuleIntrospector.Introspect(
                new BinaryModuleIntrospectionRequest("C:/modules/MyMod.dll"),
                null!));
    }

    private string? TempFilePath;

    private static string BuildValidPayload() =>
        JsonSerializer.Serialize(
            new BinaryIntrospectionPayload
            {
                ModuleName = "MyMod",
                ModulePath = "C:/modules/MyMod.dll",
                Commands =
                [
                    new BinaryCommandPayload
                    {
                        Name = "Get-Foo",
                        CommandType = "Cmdlet",
                        DefaultParameterSetName = string.Empty,
                        OutputType = [],
                        Aliases = [],
                        Parameters = [],
                        ParameterSets = [],
                    },
                ],
            },
            BinaryIntrospectionJsonSerializerContext.Default.BinaryIntrospectionPayload);
}

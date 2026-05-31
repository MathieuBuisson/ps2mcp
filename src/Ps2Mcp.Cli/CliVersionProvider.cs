using System;
using System.Reflection;

namespace Ps2Mcp.Cli;

internal static class CliVersionProvider
{
    internal static string DisplayVersion { get; } = CreateDisplayVersion();

    private static string CreateDisplayVersion()
    {
        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var semanticVersion = TryFormatSemanticVersion(informationalVersion);

        if (semanticVersion is not null)
        {
            return $"ps2mcp v{semanticVersion}";
        }

        var assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion is not null)
        {
            return $"ps2mcp v{FormatSemanticVersion(assemblyVersion)}";
        }

        throw new InvalidOperationException("Assembly version metadata is unavailable.");
    }

    private static string? TryFormatSemanticVersion(string? rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return null;
        }

        var metadataSeparatorIndex = rawVersion.IndexOfAny(['-', '+']);
        var versionCore = metadataSeparatorIndex >= 0 ? rawVersion[..metadataSeparatorIndex] : rawVersion;

        return Version.TryParse(versionCore, out var parsedVersion)
            ? FormatSemanticVersion(parsedVersion)
            : null;
    }

    private static string FormatSemanticVersion(Version version)
    {
        var patch = version.Build >= 0 ? version.Build : 0;
        return $"{version.Major}.{version.Minor}.{patch}";
    }
}

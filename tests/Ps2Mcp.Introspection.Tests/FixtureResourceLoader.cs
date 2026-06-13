using System;
using System.IO;
using System.Text;

namespace Ps2Mcp.Introspection.Tests;

internal static class FixtureResourceLoader
{
    internal const string BinaryMetadataMicrosoftPowerShellManagement =
        "Ps2Mcp.Introspection.Tests.Fixtures.BinaryMetadataMicrosoftPowerShellManagement.json";

    internal const string DiverseParamsModule =
        "Ps2Mcp.Introspection.Tests.Fixtures.DiverseParamsModule.psm1";

    internal static byte[] LoadBytes(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        using var stream = OpenResource(resourceName);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    internal static string LoadUtf8Text(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        using var reader = new StreamReader(OpenResource(resourceName), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static Stream OpenResource(string resourceName) =>
        typeof(FixtureResourceLoader).Assembly.GetManifestResourceStream(resourceName)
        ?? throw new FileNotFoundException($"Embedded fixture resource '{resourceName}' was not found.");
}

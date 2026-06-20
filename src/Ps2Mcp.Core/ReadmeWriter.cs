using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ps2Mcp.Core;

/// <summary>
/// Writes the generated package README describing runtime prerequisites.
/// </summary>
public static class ReadmeWriter
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// The fixed filename used for README output.
    /// </summary>
    public const string FileName = "README.md";

    /// <summary>
    /// Writes a deterministic README into the specified output directory.
    /// </summary>
    /// <param name="moduleName">The source module name.</param>
    /// <param name="requiredModules">Runtime prerequisites declared by the source manifest.</param>
    /// <param name="outputDirectory">The directory where <c>README.md</c> will be written.</param>
    /// <returns>The full path of the written README file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="moduleName"/> or <paramref name="outputDirectory"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="moduleName"/> or <paramref name="outputDirectory"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="requiredModules"/> is null.</exception>
    /// <exception cref="ArgumentException">An item in <paramref name="requiredModules"/> is null, empty, or whitespace.</exception>
    public static string Write(string moduleName, IReadOnlyList<string> requiredModules, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(requiredModules);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        foreach (var requiredModule in requiredModules)
        {
            if (requiredModule is null)
                throw new ArgumentNullException(nameof(requiredModules));
            if (string.IsNullOrWhiteSpace(requiredModule))
                throw new ArgumentException("Required module name cannot be empty or whitespace.", nameof(requiredModules));
        }

        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullOutputDirectory);

        var readmePath = Path.Combine(fullOutputDirectory, FileName);
        var content = BuildContent(moduleName, requiredModules);
        File.WriteAllText(readmePath, content, Utf8NoBom);
        return readmePath;
    }

    private static string BuildContent(string moduleName, IReadOnlyList<string> requiredModules)
    {
        var lines = new List<string>
        {
            $"# {EscapeMarkdown(moduleName)} prerequisites",
            string.Empty,
            "This generated package bundles the source PowerShell module but does not bundle upstream PowerShell dependencies.",
            string.Empty,
        };

        if (requiredModules.Count == 0)
        {
            lines.Add("No additional PowerShell modules are declared via RequiredModules.");
        }
        else
        {
            lines.Add("Install these PowerShell modules before running the generated MCP server:");
            lines.Add(string.Empty);

            foreach (var requiredModule in requiredModules)
            {
                lines.Add($"- {EscapeMarkdown(requiredModule)}");
            }
        }

        return string.Join("\n", lines) + "\n";
    }

    private static string EscapeMarkdown(string value) =>
        value.Replace("\\", "\\\\")
             .Replace("\r", "")
             .Replace("\n", "");
}

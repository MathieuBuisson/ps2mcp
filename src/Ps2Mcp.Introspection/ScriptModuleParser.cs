using System;
using System.Collections.Immutable;
using System.IO;
using System.Management.Automation.Language;

namespace Ps2Mcp.Introspection;

/// <summary>
/// Parses a PowerShell script module file (.psm1) into a <see cref="System.Management.Automation.Language.ScriptBlockAst"/>.
/// </summary>
/// <remarks>
/// AST parsing is the AOT-safe extraction primitive. Parse errors are surfaced as a value
/// (not thrown) so the orchestrator (<c>ScriptModuleIntrospector</c>) can decide whether partial ASTs
/// are still useful or fatal.
/// </remarks>
public static class ScriptModuleParser
{
    /// <summary>
    /// Parses a PowerShell script module file (.psm1) and returns an AST along with any parse errors.
    /// </summary>
    /// <param name="filePath">Full path to the .psm1 file.</param>
    /// <returns>A parse result containing the AST and any syntax errors.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist at <paramref name="filePath"/>.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a component of <paramref name="filePath"/> does not point to an existing directory.</exception>
    /// <exception cref="PathTooLongException">Thrown when <paramref name="filePath"/> exceeds the platform's path-length limit.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the file exists but the caller lacks read permission.</exception>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="filePath"/> contains a format (such as a colon mid-segment) that the platform cannot read.</exception>
    /// <exception cref="IOException">Thrown for other I/O failures, such as the file being locked by another process.</exception>
    /// <remarks>The AST may be partial if syntax errors are present. Callers should check <see cref="ScriptModuleParseResult.HasErrors"/>.</remarks>
    public static ScriptModuleParseResult Parse(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Script module file not found.", filePath);

        var ast = Parser.ParseFile(filePath, out _, out var errors);
        return new ScriptModuleParseResult(filePath, ast, errors.ToImmutableArray());
    }
}

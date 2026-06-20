using System.Collections.Immutable;
using System.Management.Automation.Language;

namespace Ps2Mcp.Introspection;

/// <summary>
/// Result of parsing a script module file via <see cref="ScriptModuleParser.Parse"/>.
/// </summary>
/// <remarks>
/// The <see cref="Ast"/> is always present (<c>Parser.ParseFile</c> returns a partial tree even on
/// syntax errors); <see cref="Errors"/> is empty when the source is syntactically valid.
/// </remarks>
public sealed record ScriptModuleParseResult(
    string FilePath,
    ScriptBlockAst Ast,
    ImmutableArray<ParseError> Errors,
    ImmutableArray<Token> Tokens = default)
{
    /// <summary>
    /// Gets a value indicating whether the parser reported any syntax errors.
    /// </summary>
    public bool HasErrors => !Errors.IsDefaultOrEmpty;
}

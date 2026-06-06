using System;
using System.Collections.Immutable;
using System.Management.Automation.Language;

namespace Ps2Mcp.Introspection;

/// <summary>
/// Extracts the comment-based help block from a PowerShell function definition into a
/// <see cref="CommandHelpInfo"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="FunctionDefinitionAst.GetHelpContent"/> to leverage PowerShell's own
/// comment-based help parser. This means edge cases (multiple help blocks, the
/// <c>.EXTERNALHELP</c> directive, the various recognized keywords) are handled exactly as
/// PowerShell handles them at runtime, and the extractor never invents a help-block
/// interpretation that disagrees with the runtime. Returns <c>null</c> when the function has
/// no comment-based help at all.
/// </remarks>
public static class CommandHelpExtractor
{
    /// <summary>
    /// Extracts the comment-based help from the given function definition.
    /// </summary>
    /// <param name="function">The function whose help is to be extracted.</param>
    /// <returns>A populated <see cref="CommandHelpInfo"/>, or <c>null</c> when the function
    /// has no comment-based help block.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is <c>null</c>.</exception>
    public static CommandHelpInfo? Extract(FunctionDefinitionAst function)
    {
        ArgumentNullException.ThrowIfNull(function);

        var help = function.GetHelpContent();
        if (help is null)
        {
            return null;
        }

        return new CommandHelpInfo(
            NullIfEmpty(help.Synopsis),
            NullIfEmpty(help.Description),
            ExtractParameters(help),
            ExtractExamples(help));
    }

    // PowerShell's help parser reports absent sub-blocks as empty strings and trims
    // nothing (it preserves the syntactic whitespace from the help block). The orchestrator
    // wants to distinguish "not declared" from "declared with no content", and the
    // downstream consumers do not need the trailing newlines. Collapse empty-or-whitespace
    // to null and trim the rest at the extraction boundary.
    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ImmutableArray<CommandHelpInfo.ParameterHelp> ExtractParameters(CommentHelpInfo help)
    {
        if (help.Parameters is null || help.Parameters.Count == 0)
        {
            return ImmutableArray<CommandHelpInfo.ParameterHelp>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<CommandHelpInfo.ParameterHelp>(help.Parameters.Count);
        foreach (var p in help.Parameters)
        {
            builder.Add(new CommandHelpInfo.ParameterHelp(
                p.Key,
                NullIfEmpty(p.Value)));
        }
        return builder.ToImmutable();
    }

    private static ImmutableArray<string> ExtractExamples(CommentHelpInfo help)
    {
        if (help.Examples is null || help.Examples.Count == 0)
        {
            return ImmutableArray<string>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<string>(help.Examples.Count);
        foreach (var code in help.Examples)
        {
            var trimmed = NullIfEmpty(code);
            if (trimmed is not null)
            {
                builder.Add(trimmed);
            }
        }
        return builder.ToImmutable();
    }
}

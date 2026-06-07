using System.Collections.Immutable;
using System.Linq;

namespace Ps2Mcp.Introspection;

/// <summary>
/// Captures the comment-based help block attached to a PowerShell function definition.
/// </summary>
/// <remarks>
/// All fields are optional because PowerShell allows partial help blocks. A function with no
/// comment-based help at all is represented as a null reference by
/// <see cref="CommandHelpExtractor.Extract"/>; the caller should check for null before reading
/// any field.
/// <para>
/// <see cref="Synopsis"/> and <see cref="Description"/> are null when the corresponding help
/// block is absent. PowerShell's parser produces empty strings for absent sub-blocks; the
/// extractor collapses empty-to-null to make the tri-state distinction (absent / present-
/// with-content / present-but-empty) explicit for downstream consumers.
/// </para>
/// </remarks>
public sealed record CommandHelpInfo(
    string? Synopsis,
    string? Description,
    ImmutableArray<CommandHelpInfo.ParameterHelp> Parameters,
    ImmutableArray<string> Examples)
{
    /// <summary>
    /// Gets a value indicating whether the function declares a .SYNOPSIS block.
    /// </summary>
    public bool HasSynopsis => Synopsis is not null;

    /// <summary>
    /// Gets a value indicating whether the function declares a .DESCRIPTION block.
    /// </summary>
    public bool HasDescription => Description is not null;

    /// <summary>
    /// Gets a value indicating whether the function declares at least one .PARAMETER block.
    /// This is true even when every declared block has an empty description; see
    /// <see cref="HasParameterDescriptions"/> for a stricter check.
    /// </summary>
    public bool HasParameters => !Parameters.IsDefaultOrEmpty;

    /// <summary>
    /// Gets a value indicating whether at least one declared .PARAMETER block has a non-null
    /// description. This is a stricter check than <see cref="HasParameters"/>, which is true
    /// whenever a .PARAMETER block is declared regardless of whether its description is empty.
    /// </summary>
    public bool HasParameterDescriptions => Parameters.Any(p => p.Description is not null);

    /// <summary>
    /// Gets a value indicating whether the function declares at least one .EXAMPLE block.
    /// </summary>
    public bool HasExamples => !Examples.IsDefaultOrEmpty;

    /// <summary>
    /// Captures a single .PARAMETER help entry: the parameter name (as written in the help
    /// block, not validated against the function's actual parameter list) and the description
    /// text following the name.
    /// </summary>
    public sealed record ParameterHelp(string Name, string? Description);
}

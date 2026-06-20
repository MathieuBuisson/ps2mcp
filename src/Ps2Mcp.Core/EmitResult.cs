using System.Collections.Immutable;

namespace Ps2Mcp.Core;

/// <summary>
/// Represents the output of a server emitter, containing the files to be written to disk.
/// </summary>
/// <param name="Files">The files produced by the emitter.</param>
public sealed record EmitResult(ImmutableArray<EmittedFile> Files)
{
    /// <summary>
    /// A singleton instance representing an emitter that produces no files.
    /// </summary>
    public static EmitResult Empty { get; } = new(ImmutableArray<EmittedFile>.Empty);
}

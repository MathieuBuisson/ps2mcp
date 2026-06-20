using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using Ps2Mcp.Core.Internal;

namespace Ps2Mcp.Core;

/// <summary>
/// Represents a complete MCP server manifest describing a module, its tools, IR version, and content hash.
/// </summary>
/// <param name="Module">The PowerShell module metadata.</param>
/// <param name="Tools">The tool definitions exposed by this manifest.</param>
/// <param name="IrVersion">The intermediate representation schema version.</param>
/// <param name="ContentHash">A SHA-256 hash of the module source content for cache invalidation.</param>
public sealed record ManifestDefinition(
    [property: JsonPropertyOrder(1)] ModuleDefinition Module,
    [property: JsonPropertyOrder(2)] ImmutableArray<ManifestToolDefinition> Tools,
    [property: JsonPropertyOrder(3)] int IrVersion,
    [property: JsonPropertyOrder(4)] string ContentHash)
{
    /// <summary>
    /// Creates a manifest from a server definition and content hash.
    /// </summary>
    /// <param name="server">The server definition to convert.</param>
    /// <param name="contentHash">The SHA-256 content hash of the module source.</param>
    /// <returns>A new manifest instance.</returns>
    public static ManifestDefinition FromServer(McpServerDefinition server, string contentHash)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        return new ManifestDefinition(
            Module: server.Module,
            Tools: (server.Tools.IsDefault ? ImmutableArray<ToolDefinition>.Empty : server.Tools)
                .Select(ManifestToolDefinition.FromTool).ToImmutableArray(),
            IrVersion: server.IrVersion,
            ContentHash: contentHash);
    }

    public bool Equals(ManifestDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Module == other.Module
            && SequenceEqualityHelpers.SequenceEqual(Tools, other.Tools)
            && IrVersion == other.IrVersion
            && ContentHash == other.ContentHash;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Module);
        SequenceEqualityHelpers.AddToHash(ref hash, Tools);
        hash.Add(IrVersion);
        hash.Add(ContentHash);
        return hash.ToHashCode();
    }
}

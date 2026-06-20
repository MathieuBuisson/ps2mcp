using System.Threading;
using System.Threading.Tasks;

namespace Ps2Mcp.Core;

/// <summary>
/// Generates MCP server source files from a PowerShell module definition.
/// </summary>
public interface IServerEmitter
{
    /// <summary>
    /// Asynchronously generates server source files from the given module definition.
    /// </summary>
    /// <param name="server">The MCP server definition describing the module and its tools.</param>
    /// <param name="options">Configuration options for the emitted output.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The generated files ready to be written to disk.</returns>
    Task<EmitResult> EmitAsync(
        McpServerDefinition server,
        EmitOptions options,
        CancellationToken cancellationToken = default);
}

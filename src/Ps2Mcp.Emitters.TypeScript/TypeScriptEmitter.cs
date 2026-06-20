using System;
using System.Threading;
using System.Threading.Tasks;
using Ps2Mcp.Core;

namespace Ps2Mcp.Emitters.TypeScript;

public sealed class TypeScriptEmitter : IServerEmitter
{
    public Task<EmitResult> EmitAsync(
        McpServerDefinition server,
        EmitOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BundledModuleImportPath);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(EmitResult.Empty);
    }
}

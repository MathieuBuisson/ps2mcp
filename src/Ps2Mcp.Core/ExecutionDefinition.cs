namespace Ps2Mcp.Core;

// Profile path is intentionally absent: §19 makes it a runtime argument supplied by the generated server,
// not a source-derived field. Keeping it here would break cross-environment manifest.json verification.
public sealed record ExecutionDefinition(
    int SerializationDepth);

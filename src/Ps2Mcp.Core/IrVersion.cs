namespace Ps2Mcp.Core;

// The current IR format version. Bump Current when a non-backward-compatible change to McpServerDefinition
// (or any nested record) is introduced; verify-mode reads this to distinguish intentional evolution from drift.
public static class IrVersion
{
    public const int Current = 1;
}

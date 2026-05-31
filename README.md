# ps2mcp

`ps2mcp` is a compiler-style CLI that turns an existing PowerShell module into a runnable [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server package. Point it at a local `.psd1` or `.psm1`, pick a target language, and get back a generated MCP server that exposes every exported command as a typed, schema-validated tool.

The compiler is a development-time artifact. The generated MCP server is the runtime artifact your agent client talks to.

## Status

Pre-v1, in active design and implementation. See [ai-generated/project_plan.md](ai-generated/project_plan.md) for the current phase.

## Why

Wrapping a PowerShell module as an MCP server by hand means manually mapping every parameter type to JSON Schema, handling validation attributes, managing `pwsh` invocation, serializing output, and dealing with secrets. `ps2mcp` eliminates that wrapper layer so existing PowerShell automation can be exposed to agents without per-module hand-written code.

## What It Does

- Analyzes a local PowerShell module (script or binary) via a hybrid introspection pipeline:
  - AST parsing for `.psm1` / `.ps1`
  - Out-of-process `pwsh` metadata inspection for binary modules
- Builds a language-agnostic intermediate representation of the module's tool surface.
- Emits a runnable MCP server package for one target per invocation:
  - **TypeScript** — `index.ts`, `package.json`, `tsconfig.json` (TS source only, runs on a current Node.js LTS with native TypeScript execution).
  - **Python** — `server.py`, `pyproject.toml`.
- Bundles the source PowerShell module into the generated package so the generated runtime is self-contained and immune to drift from globally installed modules.
- Emits a deterministic `manifest.json` used by `ps2mcp verify` for schema-drift detection in CI.

## Supported Platforms

- Compiler: Windows, Linux, macOS (Native AOT single-file binary).
- Generated runtime hosts: any OS with `pwsh` 7.x installed.
- `pwsh` 7.x is required on both the host running `ps2mcp` and the host running the generated server.

## Install

Release artifacts will be published per OS as standalone binaries (no .NET runtime required). Until v1 ships, build from source:

```bash
git clone https://github.com/your-org/ps2mcp.git
cd ps2mcp
dotnet publish src/Ps2Mcp.Cli -c Release -r <rid> --self-contained
```

Replace `<rid>` with `win-x64`, `linux-x64`, `osx-arm64`, etc.

## Usage

Generate a TypeScript MCP server:

```bash
ps2mcp build ./MyModule.psd1 --target typescript --out ./dist-ts
```

Generate a Python MCP server:

```bash
ps2mcp build ./MyModule.psd1 --target python --out ./dist-py
```

Verify that committed generated artifacts match the current PowerShell source (for CI build gates):

```bash
ps2mcp verify ./MyModule.psd1 --target typescript --out ./dist-ts
```

### Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Fatal usage, analysis, generation, or validation error |
| `2` | Verification drift detected |

## Generated Package Layout

```text
<out>/
├── package.json | pyproject.toml
├── tsconfig.json            # TypeScript target only
├── manifest.json
└── src/
    ├── index.ts | server.py
    └── modules/
        └── <ModuleName>/
            ├── <ModuleName>.psd1
            └── <ModuleName>.psm1
```

## Runtime Behavior

The generated server registers one MCP tool per exported PowerShell command. Each tool call:

1. Validates input arguments against the generated schema.
2. Optionally dot-sources a user-provided bootstrap profile (see below).
3. Spawns `pwsh -NoProfile -NonInteractive -Command ...`.
4. `Import-Module -Force`s the bundled module via a path relative to the generated runtime.
5. Invokes the target command and serializes results with `ConvertTo-Json -Depth 4` (overridable per tool).
6. Returns a structured success or structured error to the MCP client.

The runtime is stateless between calls in v1 and uses `stdio` transport.

### Bootstrap Profile

End users can supply a PowerShell profile to establish ambient context (for example, authentication) without modifying the source module. In the MCP client config:

```json
{
  "mcpServers": {
    "my-infra-tools": {
      "command": "node",
      "args": [
        "./dist-ts/src/index.ts",
        "--profile", "C:/Users/me/mcp-auth-profile.ps1"
      ],
      "env": {
        "AZURE_CLIENT_SECRET": "..."
      }
    }
  }
}
```

### Security

- `SecureString` parameters are converted inside `pwsh` and never appear in logs, error payloads, or schema examples.
- Audit logs record that a secure parameter was supplied, never its content.
- Generated runtimes avoid web service dependencies and remain auditable.

## Documentation

- Full specification: [ai-generated/ps2mcp-specification.md](ai-generated/ps2mcp-specification.md)
- Implementation plan: [ai-generated/project_plan.md](ai-generated/project_plan.md)
- Discovery report: [ai-generated/ps2mcp-discovery-report.md](ai-generated/ps2mcp-discovery-report.md)

## License

See [LICENSE](LICENSE).

# ps2mcp — Project Instructions

`ps2mcp` is a .NET 10 Native-AOT C# CLI that compiles a local PowerShell module into a runnable MCP server package for TypeScript or Python targets.

## Source Of Truth

- The authoritative project specification and implementation plan live in the workspace's local `ai-generated/` directory. These files are not committed to source control; treat them as read-only references when present and never link to them from committed files.
- If code and the local spec disagree, the spec wins: update the code, or update the spec explicitly. Never silently diverge.
- Work in plan order. Each code-adding task has an immediately-following test task; do both in the same change.

## Hard Rules

- **AOT-safe only.** No in-process hosting of the PowerShell engine, no reflection-emit, no dynamic code generation, no `BinaryFormatter`. Use `System.Management.Automation.Language` AST parsing for script modules and out-of-process `pwsh` for binary modules.
- **`pwsh` 7.x is the only PowerShell baseline.** Never branch into Windows PowerShell 5.1 as a primary path; legacy modules are bridged via `Import-Module -UseWindowsPowerShell`.
- **IR is language-agnostic.** Analysis layers must not contain TypeScript- or Python-specific branches. Target specifics live only in `Ps2Mcp.Emitters.*`.
- **Generated TypeScript is `.ts` source only.** The compiler must never emit pre-compiled JavaScript. The generated Python target uses `pyproject.toml` only — no `requirements.txt`.
- **Bundle the source module.** Generated runtimes load PowerShell via a path relative to their own file, never from a globally installed module.
- **Secrets never leak.** `SecureString` values are converted inside `pwsh` and must not appear in logs, errors, schema examples, or progress messages.
- **Determinism.** Manifest and IR serialization must be byte-identical across OSes and runs (stable key ordering, invariant culture). `verify` depends on this.
- **Exit codes are reserved.** `0` success, `1` fatal, `2` drift. Don't invent new codes.

## Development Discipline

- **Test in the same change as the code.** Every code-adding task in the plan has a paired test task. Skipping the test task is not allowed.
- **No over-engineering.** No helpers, abstractions, fallbacks, or feature flags beyond what the spec requires.
- **No third-party linters or style packages.** Lint is the in-box .NET analyzers + `dotnet format`. No StyleCop, no SonarAnalyzer, no Husky.
- **No comments that restate code.** Only comment hidden constraints, AOT-specific workarounds, or non-obvious invariants — and keep them to one line.

## Build And Test

```pwsh
dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet publish src/Ps2Mcp.Cli -c Release -r <rid> --self-contained /p:PublishAot=true
```

## Layout

```text
src/
  Ps2Mcp.Cli/            # CLI entry, exit-code mapping, host preflight
  Ps2Mcp.Core/           # IR, manifest, deterministic JSON
  Ps2Mcp.Introspection/  # AST + out-of-process pwsh introspection
  Ps2Mcp.Emitters.TypeScript/
  Ps2Mcp.Emitters.Python/
tests/
  <mirror of src/>
  runtime-ts/   # Node LTS conformance harness for generated TS servers
  runtime-py/   # Python conformance harness for generated Python servers
  fixtures/modules/  # script + binary module fixtures
```

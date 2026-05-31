---
description: "Use when writing or modifying C# code in ps2mcp. Covers .NET 10 Native AOT constraints, analyzer rules, file-scoped namespace style, and PowerShell SDK usage boundaries."
applyTo: "**/*.cs"
---

# C# Conventions (ps2mcp)

## AOT Compatibility (Hard)

- `Ps2Mcp.Cli` and every assembly it transitively references must remain Native-AOT compatible.
- Forbidden APIs in AOT-reachable code:
  - `System.Reflection.Emit`
  - `Assembly.LoadFrom` / dynamic plugin loading
  - `BinaryFormatter`, `SoapFormatter`
  - In-process PowerShell hosting (`PowerShell.Create()`, `Runspace*`, `InitialSessionState`)
  - `JsonSerializer` overloads that require reflection-based metadata — use `System.Text.Json` source-generated `JsonSerializerContext`
- Annotate AOT-incompatible test-only code with `[RequiresDynamicCode]` / `[RequiresUnreferencedCode]` and keep it out of `Ps2Mcp.Cli`'s dependency graph.
- `<IsAotCompatible>true</IsAotCompatible>` must be set on every project that ships in the CLI. IL2xxx/IL3xxx warnings fail the build.

## PowerShell SDK Boundary

- `Microsoft.PowerShell.SDK` is referenced **only** from `Ps2Mcp.Introspection` for AST parsing types in `System.Management.Automation.Language` (parser, AST nodes, attribute AST). Do not call into the engine.
- Binary-module introspection invokes `pwsh` out-of-process via the `PwshRunner` abstraction.

## Style

- File-scoped namespaces.
- `var` for built-in and apparent types; explicit type when it aids readability.
- `record` / `record struct` for IR types; init-only properties; non-nullable by default.
- `sealed` by default on concrete classes.
- Async methods end in `Async` and accept `CancellationToken` as the last parameter.
- No `#region`. No `this.` qualification. No `System.*` aliases.
- Match brace and qualification rules in [.editorconfig](../../.editorconfig) — `dotnet format --verify-no-changes` must pass.

## Error Handling

- Throw specific exceptions; do not swallow. The CLI layer maps to exit codes `0` (success), `1` (fatal), and `2` (drift). Lower layers don't call `Environment.Exit`.
- Validate at boundaries only (CLI args, file inputs, `pwsh` output). Trust internal invariants.

## Determinism

- IR and manifest serialization must produce byte-identical output across OSes and runs. Use ordinal string comparisons, invariant culture, and stable key ordering.

## Tests

- xUnit. One test project per `src/` project, mirrored layout.
- Every public method gets a test in the same change. No test-after.
- Snapshot tests for emitter output; PowerShell fixtures under `tests/fixtures/modules/`.
- Process-spawning tests use the `PwshRunner` interface with an in-memory fake, not real `pwsh`, unless the test is explicitly a runtime integration test.

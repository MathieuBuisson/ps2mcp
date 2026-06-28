This folder contains embedded snapshot artifacts for [TypeScriptEmitterTests.cs](c:/git/ps2mcp/tests/Ps2Mcp.Emitters.TypeScript.Tests/TypeScriptEmitterTests.cs).

The `.ts` files here are not runtime source files. They are expected-output fixtures used by snapshot assertions in the test project.

`RepresentativeIndex.snapshot.txt` is intentionally stored as plain text on disk even though it is embedded under the logical `.ts` resource name. This avoids TypeScript import-organization on save rewriting the full-file snapshot.

Top-level files capture larger generated slices such as the representative `index.ts`, the PowerShell driver script, and selected runtime blocks.

The `Schemas/` subfolder contains focused snapshots for individual generated Zod schema declarations.

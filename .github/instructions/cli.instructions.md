---
description: "Use when writing or modifying user-facing CLI text in Ps2Mcp.Cli. Covers quoting conventions for usage, help, and error messages."
applyTo: "src/Ps2Mcp.Cli/**/*.cs"
---

# CLI Message Conventions (Ps2Mcp.Cli)

- For user-facing CLI text, wrap literal CLI tokens defined by the tool in backticks.
- For echoed user input, wrap the echoed value in single quotes.
- Keep new usage, help, version, and error text aligned with the existing CLI wording unless the spec requires a broader wording change.
- Apply this convention consistently across stdout and stderr output in `Ps2Mcp.Cli`.

Examples:

- `` `--target` is required. ``
- `Unknown command 'publish'.`

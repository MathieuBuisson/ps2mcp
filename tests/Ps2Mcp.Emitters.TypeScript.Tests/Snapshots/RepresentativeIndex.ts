import { spawn } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";

const runtimeDirectory = dirname(fileURLToPath(import.meta.url));
const bundledModuleImportPath = resolve(runtimeDirectory, "./modules/Demo.Module/Demo.Module.psd1");
const invokePowerShellCommandScript = [
  "$ErrorActionPreference = 'Stop'",
  "$modulePath = $env:PS2MCP_MODULE_PATH",
  "$sourceCommand = $env:PS2MCP_SOURCE_COMMAND",
  "$serializationDepth = [int]$env:PS2MCP_SERIALIZATION_DEPTH",
  "$argumentsJson = [Console]::In.ReadToEnd()",
  "$arguments = if ([string]::IsNullOrWhiteSpace($argumentsJson)) { @{} } else { ConvertFrom-Json -InputObject $argumentsJson -AsHashtable }",
  "Import-Module -Force $modulePath",
  "$result = & $sourceCommand @arguments",
  "$result | ConvertTo-Json -Depth $serializationDepth -Compress",
].join("; ");

const DEFAULT_TIMEOUT_MS = 30000;

const getDemoItemInputSchema = z.object({
  Name: z.string(),
  Tags: z.array(z.string()).optional(),
});

const server = new McpServer({
  name: "Demo.Module",
  version: "1.2.3",
});

server.registerTool(
  "get_demo_item",
  {
    description: "Gets a demo item.",
    inputSchema: getDemoItemInputSchema,
  },
  async (args) => invokePowerShellTool("Get-DemoItem", args, 4, 30000),
);

async function invokePowerShellTool(
  sourceCommand: string,
  args: unknown,
  serializationDepth: number,
  timeoutMs: number,
): Promise<{ content: Array<{ type: "text"; text: string }> }> {
  const argsJson = args === undefined ? "{}" : JSON.stringify(args);
  const child = spawn(
    "pwsh",
    [
      "-NoProfile",
      "-NonInteractive",
      "-Command",
      invokePowerShellCommandScript,
    ],
    {
      env: {
        ...process.env,
        PS2MCP_MODULE_PATH: bundledModuleImportPath,
        PS2MCP_SERIALIZATION_DEPTH: serializationDepth.toString(10),
        PS2MCP_SOURCE_COMMAND: sourceCommand,
      },
      stdio: ["pipe", "pipe", "pipe"],
    },
  );
  child.stdin.end(argsJson, "utf8");

  child.stdout.setEncoding("utf8");
  child.stderr.setEncoding("utf8");

  const stdoutChunks: string[] = [];
  const stderrChunks: string[] = [];
  child.stdout.on("data", (chunk: string) => stdoutChunks.push(chunk));
  child.stderr.on("data", (chunk: string) => stderrChunks.push(chunk));

  const exitCode = await Promise.race([
    new Promise<number | null>((resolveClose, reject) => {
      child.once("error", reject);
      child.once("close", (code) => resolveClose(code));
    }),
    new Promise<never>((_resolve, reject) => {
      setTimeout(() => {
        child.kill();
        reject(new Error(
          `PowerShell invocation for ${sourceCommand} exceeded timeout of ${timeoutMs}ms and was terminated.`,
        ));
      }, timeoutMs);
    }),
  ]);

  if ((exitCode ?? 1) !== 0) {
    const stderr = stderrChunks.join("").trim();
    throw new Error(
      `PowerShell invocation for ${sourceCommand} failed with exit code ${exitCode ?? "null"}: ${stderr}`,
    );
  }

  const stdout = stdoutChunks.join("").trim();
  const stderr = stderrChunks.join("").trim();
  const content: Array<{ type: "text"; text: string }> = [
    { type: "text", text: stdout.length === 0 ? "null" : stdout },
  ];
  if (stderr.length > 0) {
    content.push({ type: "text", text: stderr });
  }
  return { content };
}

async function main(): Promise<void> {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

void main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});

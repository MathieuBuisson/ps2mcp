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
  "$profilePath = $env:PS2MCP_PROFILE_PATH",
  "$sourceCommand = $env:PS2MCP_SOURCE_COMMAND",
  "$serializationDepth = [int]$env:PS2MCP_SERIALIZATION_DEPTH",
  "$argumentsJson = [Console]::In.ReadToEnd()",
  "$arguments = if ([string]::IsNullOrWhiteSpace($argumentsJson)) { @{} } else { ConvertFrom-Json -InputObject $argumentsJson -AsHashtable }",
  "if (-not [string]::IsNullOrWhiteSpace($profilePath)) {",
  "if (-not (Test-Path -LiteralPath $profilePath -PathType Leaf)) {",
  "throw \"Bootstrap profile file not found: $profilePath\"",
  "}",
  "try {",
  ". $profilePath",
  "}",
  "catch {",
  "throw \"Bootstrap profile failed: $($_.Exception.Message)\"",
  "}",
  "}",
  "Import-Module -Force $modulePath",
  "$result = & $sourceCommand @arguments",
  "$result | ConvertTo-Json -Depth $serializationDepth -Compress",
].join("; ");

type RuntimeOptions = {
  profilePath?: string;
};

function parseRuntimeOptions(argv: string[]): RuntimeOptions {
  let profilePath: string | undefined;

  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === "--profile") {
      if (profilePath !== undefined) {
        throw new Error("Runtime argument \"--profile\" may be specified at most once.");
      }

      index += 1;
      const value = argv[index];
      if (value === undefined || value.length === 0) {
        throw new Error("Runtime argument \"--profile\" requires a path value.");
      }

      profilePath = value;
      continue;
    }

    throw new Error(`Unknown runtime argument: ${argument}`);
  }

  return { profilePath };
}

const runtimeOptions = parseRuntimeOptions(process.argv.slice(2));

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
  async (args) => invokePowerShellTool("Get-DemoItem", args, 4, 30000, runtimeOptions.profilePath),
);

async function invokePowerShellTool(
  sourceCommand: string,
  args: unknown,
  serializationDepth: number,
  timeoutMs: number,
  profilePath: string | undefined,
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
        PS2MCP_PROFILE_PATH: profilePath ?? "",
        PS2MCP_SERIALIZATION_DEPTH: serializationDepth.toString(10),
        PS2MCP_SOURCE_COMMAND: sourceCommand,
      },
      stdio: ["pipe", "pipe", "pipe"],
    },
  );

  child.stdout.setEncoding("utf8");
  child.stderr.setEncoding("utf8");

  const stdoutChunks: string[] = [];
  const stderrChunks: string[] = [];
  child.stdout.on("data", (chunk: string) => stdoutChunks.push(chunk));
  child.stderr.on("data", (chunk: string) => stderrChunks.push(chunk));

  const exitCode = await new Promise<number | null>((resolveClose, reject) => {
    const onError = (err: Error) => {
      cleanup();
      reject(err);
    };
    const onClose = (code: number | null) => {
      cleanup();
      resolveClose(code);
    };
    const timer = setTimeout(() => {
      cleanup();
      child.kill();
      reject(new Error(
        `PowerShell invocation for ${sourceCommand} exceeded timeout of ${timeoutMs}ms and was terminated.`,
      ));
    }, timeoutMs);
    const cleanup = () => {
      clearTimeout(timer);
      child.stdin.off("error", onError);
      child.off("error", onError);
      child.off("close", onClose);
    };
    child.stdin.on("error", onError);
    child.stdin.end(argsJson, "utf8");
    child.once("error", onError);
    child.once("close", onClose);
  });

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

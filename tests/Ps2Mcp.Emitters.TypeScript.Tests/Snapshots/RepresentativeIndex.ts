import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";

const bundledModuleImportPath = "./modules/Demo.Module/Demo.Module.psd1";

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
    title: "Get-DemoItem",
    description: "Gets a demo item.",
    inputSchema: getDemoItemInputSchema.shape,
  },
  async (args) => invokePowerShellTool("Get-DemoItem", args, 4),
);

async function invokePowerShellTool(
  sourceCommand: string,
  args: unknown,
  serializationDepth: number,
): Promise<{ content: Array<{ type: "text"; text: string }> }> {
  void args;
  throw new Error(
    `PowerShell invocation for ${sourceCommand} is not implemented yet. Bundled module path: ${bundledModuleImportPath}. Serialization depth: ${serializationDepth}.`,
  );
}

async function main(): Promise<void> {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

void main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});

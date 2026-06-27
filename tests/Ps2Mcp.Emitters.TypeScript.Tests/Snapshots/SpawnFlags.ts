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
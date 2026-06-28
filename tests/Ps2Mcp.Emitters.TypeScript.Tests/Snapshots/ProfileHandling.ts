// Snapshot artifact: auto-generated test fixture. Not runtime source.
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

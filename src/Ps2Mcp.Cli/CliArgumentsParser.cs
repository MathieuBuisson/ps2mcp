using System;
using System.Diagnostics.CodeAnalysis;

namespace Ps2Mcp.Cli;

internal static class CliArgumentsParser
{
    private static readonly StringComparer KeywordComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly StringComparer OptionValueComparer = StringComparer.Ordinal;

    internal const string UsageText = """
Usage:
    ps2mcp <build|verify> <module-path> (--target|-t) <typescript|python> (--out|-o) <directory>
  ps2mcp [--help|-h]
  ps2mcp [--version|-v]
""";

    internal static bool TryParse(
        string[] args,
        [NotNullWhen(true)] out CliParseResult? parseResult,
        [NotNullWhen(false)] out string? errorMessage)
    {
        parseResult = null;
        errorMessage = null;

        if (ContainsFlag(args, "--version", "-v"))
        {
            parseResult = CliParseResult.Version;
            return true;
        }

        if (ContainsFlag(args, "--help", "-h"))
        {
            parseResult = CliParseResult.Help;
            return true;
        }

        if (args.Length == 0)
        {
            errorMessage = "A command is required.";
            return false;
        }

        if (!TryParseCommand(args[0], out var command))
        {
            errorMessage = $"Unknown command '{args[0]}'. Supported commands: build, verify.";
            return false;
        }

        string? modulePath = null;
        string? outputDirectory = null;
        GenerationTarget? target = null;

        for (var index = 1; index < args.Length; index++)
        {
            var token = args[index];

            if (MatchesKeyword(token, "--target", "-t"))
            {
                if (target is not null)
                {
                    errorMessage = "`--target` must be specified exactly once.";
                    return false;
                }

                if (!TryReadOptionValue(args, ref index, token, out var targetValue, out errorMessage))
                {
                    return false;
                }

                if (!TryParseTarget(targetValue, out var parsedTarget))
                {
                    errorMessage = "`--target` must be either 'typescript' or 'python'.";
                    return false;
                }

                target = parsedTarget;
                continue;
            }

            if (MatchesKeyword(token, "--out", "-o"))
            {
                if (outputDirectory is not null)
                {
                    errorMessage = "`--out` must be specified exactly once.";
                    return false;
                }

                if (!TryReadOptionValue(args, ref index, token, out outputDirectory, out errorMessage))
                {
                    return false;
                }

                continue;
            }

            if (IsOption(token))
            {
                errorMessage = $"Unknown option '{token}'.";
                return false;
            }

            if (modulePath is null)
            {
                modulePath = token;
                continue;
            }

            errorMessage = $"Unexpected argument '{token}'.";
            return false;
        }

        if (modulePath is null)
        {
            errorMessage = "A module path is required.";
            return false;
        }

        if (target is null)
        {
            errorMessage = "`--target` is required.";
            return false;
        }

        if (outputDirectory is null)
        {
            errorMessage = "`--out` is required.";
            return false;
        }

        parseResult = CliParseResult.ForInvocation(new CliInvocation(command, modulePath, target.Value, outputDirectory));
        return true;
    }

    private static bool ContainsFlag(string[] args, string longFlag, string shortFlag)
    {
        foreach (var arg in args)
        {
            if (MatchesKeyword(arg, longFlag, shortFlag))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseCommand(string value, out CliCommand command)
    {
        if (MatchesKeyword(value, "build"))
        {
            command = CliCommand.Build;
            return true;
        }

        if (MatchesKeyword(value, "verify"))
        {
            command = CliCommand.Verify;
            return true;
        }

        command = default;
        return false;
    }

    private static bool TryParseTarget(string value, out GenerationTarget target)
    {
        if (OptionValueComparer.Equals(value, "typescript"))
        {
            target = GenerationTarget.TypeScript;
            return true;
        }

        if (OptionValueComparer.Equals(value, "python"))
        {
            target = GenerationTarget.Python;
            return true;
        }

        target = default;
        return false;
    }

    private static bool MatchesKeyword(string value, string longKeyword, string? shortKeyword = null)
    {
        if (KeywordComparer.Equals(value, longKeyword))
        {
            return true;
        }

        return shortKeyword is not null && KeywordComparer.Equals(value, shortKeyword);
    }

    private static bool TryReadOptionValue(
        string[] args,
        ref int index,
        string optionName,
        [NotNullWhen(true)] out string? optionValue,
        [NotNullWhen(false)] out string? errorMessage)
    {
        if (index + 1 >= args.Length || IsOption(args[index + 1]))
        {
            optionValue = null;
            errorMessage = $"`{optionName}` requires a value.";
            return false;
        }

        index++;
        optionValue = args[index];
        errorMessage = null;
        return true;
    }

    private static bool IsOption(string value) => value.StartsWith("-", StringComparison.Ordinal);
}

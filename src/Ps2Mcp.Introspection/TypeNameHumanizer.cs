using System.Text;
using System.Text.RegularExpressions;

namespace Ps2Mcp.Introspection;

// Shared type-name humanization pipeline used by both the script-module extractor
// (which receives an ITypeName from the PowerShell AST) and the binary-module mapper
// (which receives a string from PowerShell's CommandMetadata). The humanization rules
// are identical in both cases; the input shape is the only difference.
internal static partial class TypeNameHumanizer
{
    // Pipeline applied in order:
    //   1. Strip the CLR generic-arity marker `N (backtick + digits) so List`1[string]
    //      surfaces as List[string].
    //   2. Collapse the [[ and ]] nested-generic markers to single brackets. A naive
    //      single-pass replacement over-collapses legitimate close pairs in cases like
    //      Nullable[Nullable[int]] (where each generic has a single type arg, so the
    //      FullName carries no [[ marker); a stateful scan is required to track which
    //      [[ opens have been consumed.
    //   3. Strip CLR-looking namespace prefixes (System.* and Microsoft.*) wherever
    //      they appear, including inside generic arguments (e.g. the System.String
    //      inside MyApp.Foo[System.String] becomes String). Application namespaces
    //      that happen to start with System or Microsoft (e.g. MyApp.System.Internal)
    //      are unusual and, if matched, will have the matched segment stripped. The
    //      \b word boundary prevents matching partial words (e.g. SystemX.Foo).
    //   4. No surrounding brackets are added; the output shape matches the simple-type
    //      tests (e.g. "string", "int") for consistency.
    public static string Humanize(string? fullName)
    {
        if (string.IsNullOrEmpty(fullName))
        {
            return "object";
        }

        var s = GenericArityRegex().Replace(fullName, string.Empty);
        s = CollapseNestedBrackets(s);
        s = ClrNamespacePrefixRegex().Replace(s, m => m.Value[(m.Value.LastIndexOf('.') + 1)..]);
        return s;
    }

    // Collapses the CLR nested-generic markers ([[ and ]]) to single brackets, leaving
    // legitimate close pairs untouched. A [[ collapses to [ and marks the open as
    // unconsumed; a subsequent ]] collapses to ] only when it matches the unconsumed
    // open; otherwise both ] characters are emitted verbatim. This is the only safe shape
    // for cases like Nullable[Nullable[int]] where two adjacent ] characters are
    // legitimate close pairs.
    private static string CollapseNestedBrackets(string s)
    {
        var sb = new StringBuilder(s.Length);
        var unconsumedOpen = false;
        var i = 0;
        while (i < s.Length)
        {
            var ch = s[i];
            if (ch == '[' && i + 1 < s.Length && s[i + 1] == '[')
            {
                sb.Append('[');
                unconsumedOpen = true;
                i += 2;
            }
            else if (ch == ']' && i + 1 < s.Length && s[i + 1] == ']')
            {
                if (unconsumedOpen)
                {
                    sb.Append(']');
                    unconsumedOpen = false;
                    i += 2;
                }
                else
                {
                    sb.Append(']').Append(']');
                    i += 2;
                }
            }
            else
            {
                sb.Append(ch);
                i++;
            }
        }
        return sb.ToString();
    }

    [GeneratedRegex(@"`\d+")]
    private static partial Regex GenericArityRegex();

    // Matches System.* or Microsoft.* wherever they appear in the type name,
    // including inside generic arguments. The \b word boundary prevents matching
    // partial words (e.g. SystemX.Foo or MicrosoftY.Bar). The replacement takes
    // the rightmost segment so System.String → String and
    // System.Collections.Generic.List`1 → List.
    [GeneratedRegex(@"\b(?:System|Microsoft)(?:\.\w+)+\b")]
    private static partial Regex ClrNamespacePrefixRegex();
}

using System.Linq;
using System.Management.Automation.Language;
using Ps2Mcp.Introspection;

namespace Ps2Mcp.Introspection.Tests;

public sealed class CommandHelpExtractorTests
{
    [Fact]
    public void Extract_ThrowsArgumentNullException_ForNullFunction()
    {
        Assert.Throws<ArgumentNullException>(() => CommandHelpExtractor.Extract(null!));
    }

    [Fact]
    public void Extract_ReturnsNull_WhenFunctionHasNoHelp()
    {
        // No <# ... #> block at all — the function has no comment-based help.
        var function = ParseFunction("function F { param([string] $X) }");

        Assert.Null(CommandHelpExtractor.Extract(function));
    }

    // ---- Synopsis -----------------------------------------------------------

    [Fact]
    public void Extract_SynopsisFromSynopsisBlock()
    {
        var function = ParseFunction(@"
<#
.SYNOPSIS
Gets a foo by name.
#>
function Get-Foo { param([string]$Name) }");

        var help = CommandHelpExtractor.Extract(function);

        Assert.NotNull(help);
        Assert.Equal("Gets a foo by name.", help!.Synopsis);
        Assert.True(help.HasSynopsis);
    }

    [Fact]
    public void Extract_SynopsisAndDescriptionNull_WhenOnlyParameterBlockPresent()
    {
        // Partial help: only a .PARAMETER block. Synopsis and Description must both be null
        // (not empty string) so downstream consumers can distinguish "not declared" from
        // "declared with no content".
        var function = ParseFunction(@"
<#
.PARAMETER Name
The name of the foo.
#>
function Get-Foo { param([string]$Name) }");

        var help = CommandHelpExtractor.Extract(function);

        Assert.NotNull(help);
        Assert.Null(help!.Synopsis);
        Assert.Null(help.Description);
        Assert.False(help.HasSynopsis);
        Assert.False(help.HasDescription);
        Assert.True(help.HasParameters);
        Assert.False(help.HasExamples);
        Assert.Single(help.Parameters);
        // PowerShell's help parser uppercases parameter names in .PARAMETER blocks; the
        // extractor preserves the SDK's normalization rather than re-casing.
        Assert.Equal("NAME", help.Parameters[0].Name);
        Assert.Equal("The name of the foo.", help.Parameters[0].Description);
    }

    [Fact]
    public void Extract_SynopsisNull_WhenSynopsisBlockIsEmpty()
    {
        // Malformed help: a .SYNOPSIS block declared with no content. The extractor must
        // still recognize the help block (returning a non-null CommandHelpInfo), but the
        // Synopsis field stays null per the empty-to-null collapse rule.
        var function = ParseFunction(@"
<#
.SYNOPSIS
#>
function Get-Foo { param([string]$Name) }");

        var help = CommandHelpExtractor.Extract(function);

        Assert.NotNull(help);
        Assert.Null(help!.Synopsis);
    }

    // ---- Description --------------------------------------------------------

    [Fact]
    public void Extract_DescriptionFromDescriptionBlock_PreservesNewlines()
    {
        var function = ParseFunction(@"
<#
.DESCRIPTION
First line of description.
Second line of description.
#>
function Get-Foo { param([string]$Name) }");

        var help = CommandHelpExtractor.Extract(function);

        Assert.NotNull(help);
        Assert.NotNull(help!.Description);
        Assert.Contains("First line of description.", help.Description);
        Assert.Contains("Second line of description.", help.Description);
        Assert.Contains('\n', help.Description);
    }

    // ---- Parameters ---------------------------------------------------------

    [Fact]
    public void Extract_ParametersFromParameterBlocks()
    {
        var function = ParseFunction(@"
<#
.PARAMETER Name
The name of the foo.
.PARAMETER Count
The number of foos to return.
#>
function Get-Foo { param([string]$Name, [int]$Count) }");

        var help = CommandHelpExtractor.Extract(function);

        Assert.NotNull(help);
        Assert.Equal(2, help!.Parameters.Length);
        // PowerShell's help parser uppercases parameter names in .PARAMETER blocks; the
        // extractor preserves the SDK's normalization rather than re-casing.
        Assert.Equal("NAME", help.Parameters[0].Name);
        Assert.Equal("The name of the foo.", help.Parameters[0].Description);
        Assert.Equal("COUNT", help.Parameters[1].Name);
        Assert.Equal("The number of foos to return.", help.Parameters[1].Description);
    }

    // ---- Examples -----------------------------------------------------------

    [Fact]
    public void Extract_ExamplesFromExampleBlocks()
    {
        var function = ParseFunction(@"
<#
.EXAMPLE
Get-Foo -Name 'bar'
.EXAMPLE
Get-Foo -Name 'baz' -Count 3
#>
function Get-Foo { param([string]$Name, [int]$Count) }");

        var help = CommandHelpExtractor.Extract(function);

        Assert.NotNull(help);
        Assert.Equal(2, help!.Examples.Length);
        Assert.Contains("Get-Foo -Name 'bar'", help.Examples[0]);
        Assert.Contains("Get-Foo -Name 'baz' -Count 3", help.Examples[1]);
    }

    [Fact]
    public void Extract_Examples_EmptyExampleBlockYieldsNoExample()
    {
        var function = ParseFunction(@"
<#
.EXAMPLE

#>
function Get-Foo { }");

        var help = CommandHelpExtractor.Extract(function);

        Assert.NotNull(help);
        Assert.False(help!.HasExamples);
        Assert.Empty(help.Examples);
    }

    // ---- Integration --------------------------------------------------------

    [Fact]
    public void Extract_HandlesRealisticFunction()
    {
        // Mirrors a typical production cmdlet with all four supported help blocks.
        var function = ParseFunction(@"
<#
.SYNOPSIS
Gets a foo by name.
.DESCRIPTION
This function retrieves a foo from the foo service.
It can retrieve one or many.
.PARAMETER Name
The name of the foo to retrieve.
.EXAMPLE
Get-Foo -Name 'bar'
#>
function Get-Foo { param([string]$Name) }");

        var help = CommandHelpExtractor.Extract(function);

        Assert.NotNull(help);
        Assert.Equal("Gets a foo by name.", help!.Synopsis);
        Assert.Contains("This function retrieves a foo", help.Description);
        Assert.Single(help.Parameters);
        // PowerShell's help parser uppercases parameter names in .PARAMETER blocks; the
        // extractor preserves the SDK's normalization rather than re-casing.
        Assert.Equal("NAME", help.Parameters[0].Name);
        Assert.Equal("The name of the foo to retrieve.", help.Parameters[0].Description);
        Assert.Single(help.Examples);
        Assert.Contains("Get-Foo -Name 'bar'", help.Examples[0]);
    }

    // ---- Helpers ------------------------------------------------------------

    private static FunctionDefinitionAst ParseFunction(string script)
    {
        var ast = Parser.ParseInput(script, out _, out var errors);
        Assert.Empty(errors);
        return ast.FindAll(a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true)
            .Cast<FunctionDefinitionAst>()
            .Single();
    }
}

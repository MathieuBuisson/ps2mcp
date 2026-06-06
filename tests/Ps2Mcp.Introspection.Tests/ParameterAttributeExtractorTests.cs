using System.Linq;
using System.Management.Automation.Language;
using Ps2Mcp.Introspection;

namespace Ps2Mcp.Introspection.Tests;

public sealed class ParameterAttributeExtractorTests
{
    [Fact]
    public void Extract_ThrowsArgumentNullException_ForNullParameter()
    {
        Assert.Throws<ArgumentNullException>(() => ParameterAttributeExtractor.Extract(null!));
    }

    // ---- Type extraction ----------------------------------------------------

    [Fact]
    public void Extract_TypeFromSingleTypeConstraint()
    {
        var info = ExtractFrom("function F { param([string] $Name) }", "Name");

        Assert.Equal("string", info.Type);
    }

    [Fact]
    public void Extract_TypePreservesCaseOfUserWrittenName()
    {
        var info = ExtractFrom("function F { param([SecureString] $Token) }", "Token");

        Assert.Equal("SecureString", info.Type);
    }

    [Fact]
    public void Extract_TypeFallsBackToObject_WhenNoTypeConstraintDeclared()
    {
        var info = ExtractFrom("function F { param($Name) }", "Name");

        Assert.Equal("object", info.Type);
    }

    [Fact]
    public void Extract_TypeTakesLastConstraint_WhenMultipleTypeConstraintsDeclared()
    {
        // Multiple type constraints form a right-to-left conversion chain: the rightmost constraint
        // defines the parameter's static type. Verified empirically in PowerShell:
        //   function f { param([int][string]$x) $x.GetType().Name }
        // returns "String", not "Int32".
        var info = ExtractFrom("function F { param([int][string] $Value) }", "Value");

        Assert.Equal("string", info.Type);
    }

    [Fact]
    public void Extract_TypeTakesLastConstraint_RegardlessOfOrder()
    {
        // [string][int] is the reverse of the above; the rightmost is still [int].
        var info = ExtractFrom("function F { param([string][int] $Value) }", "Value");

        Assert.Equal("int", info.Type);
    }

    [Fact]
    public void Extract_TypeTakesLastConstraint_InThreeElementChain()
    {
        // Three-element chain: only the rightmost constraint is the parameter's static type.
        var info = ExtractFrom("function F { param([int][string][double] $Value) }", "Value");

        Assert.Equal("double", info.Type);
    }

    // ---- Type humanization (CLR arity + nested brackets + namespace strip) -

    [Fact]
    public void Extract_TypeHumanizedAsListString()
    {
        // [List[string]] -> TypeName.FullName "List`1[string]" -> strip arity -> "List[string]".
        var info = ExtractFrom("function F { param([List[string]] $Value) }", "Value");

        Assert.Equal("List[string]", info.Type);
    }

    [Fact]
    public void Extract_TypeHumanizedAsNullableInt()
    {
        var info = ExtractFrom("function F { param([Nullable[int]] $Value) }", "Value");

        Assert.Equal("Nullable[int]", info.Type);
    }

    [Fact]
    public void Extract_TypeHumanizedAsDictionaryWithTwoTypeArgs()
    {
        var info = ExtractFrom("function F { param([Dictionary[string, int]] $Value) }", "Value");

        Assert.Equal("Dictionary[string,int]", info.Type);
    }

    [Fact]
    public void Extract_TypeHumanizedStripsClrNamespacePrefix()
    {
        // System.Collections.Generic.* is a CLR-looking namespace; the prefix is stripped and
        // only the rightmost type segment survives.
        var info = ExtractFrom(
            "function F { param([System.Collections.Generic.List[string]] $Value) }", "Value");

        Assert.Equal("List[string]", info.Type);
    }

    [Fact]
    public void Extract_TypeHumanizedPreservesCustomApplicationNamespace()
    {
        // MyApp.Services.Foo is not a CLR-looking prefix; it must survive humanization.
        var info = ExtractFrom("function F { param([MyApp.Services.Foo] $Value) }", "Value");

        Assert.Equal("MyApp.Services.Foo", info.Type);
    }

    [Fact]
    public void Extract_TypeHumanizedStripsInnerClrNamespaceButPreservesOuter()
    {
        // Outer application namespace is preserved; inner System.String is humanized to its
        // rightmost segment. The PowerShell alias recognition (String -> string) is intentionally
        // not done here; it is reserved for the Phase 8 PowerShellTypeMapper.
        var info = ExtractFrom(
            "function F { param([MyApp.Foo[System.String]] $Value) }", "Value");

        Assert.Equal("MyApp.Foo[String]", info.Type);
    }

    [Fact]
    public void Extract_TypeHumanizedAsArray()
    {
        var info = ExtractFrom("function F { param([int[]] $Value) }", "Value");

        Assert.Equal("int[]", info.Type);
    }

    [Fact]
    public void Extract_TypeHumanizedForNestedGeneric()
    {
        // Nested generic: the CLR FullName uses [[ and ]] markers around the inner-most arg;
        // those collapse to single brackets during humanization.
        var info = ExtractFrom("function F { param([Nullable[Nullable[int]]] $Value) }", "Value");

        Assert.Equal("Nullable[Nullable[int]]", info.Type);
    }

    // ---- IsMandatory --------------------------------------------------------

    [Theory]
    [InlineData("[Parameter(Mandatory)]", true)]              // PowerShell shorthand — synthesized as $true
    [InlineData("[Parameter(Mandatory=$true)]", true)]        // explicit $true variable
    [InlineData("[Parameter(Mandatory=$True)]", true)]        // case-insensitive variable
    [InlineData("[Parameter(Mandatory=$false)]", false)]
    [InlineData("[Parameter(Mandatory=$False)]", false)]      // case-insensitive variable, false branch
    [InlineData("[Parameter()]", false)]
    [InlineData("[Parameter(ParameterSetName='A')]", false)]  // Mandatory absent on the only [Parameter()]
    public void Extract_IsMandatory_ResolvesParameterAttribute(string attribute, bool expected)
    {
        var script = $"function F {{ param({attribute} [string] $X) }}";
        var info = ExtractFrom(script, "X");

        Assert.Equal(expected, info.IsMandatory);
    }

    [Fact]
    public void Extract_IsMandatoryTrue_WhenAnyOfMultipleParameterAttributesIsMandatory()
    {
        // The first [Parameter()] is optional in set A; the second makes the parameter mandatory in set B.
        // Per §13.3 we surface the "mandatory in any set" rule on the single IsMandatory bool.
        var script = "function F { param(" +
                     "[Parameter(ParameterSetName='A')] " +
                     "[Parameter(Mandatory, ParameterSetName='B')] " +
                     "[string] $X) }";
        var info = ExtractFrom(script, "X");

        Assert.True(info.IsMandatory);
    }

    // ---- ParameterSets ------------------------------------------------------

    [Fact]
    public void Extract_ParameterSetsEmpty_WhenNoSetNameDeclared()
    {
        var info = ExtractFrom("function F { param([string] $X) }", "X");

        Assert.True(info.ParameterSets.IsDefaultOrEmpty);
    }

    [Fact]
    public void Extract_ParameterSetsContainsSetName()
    {
        var info = ExtractFrom("function F { param([Parameter(ParameterSetName='Foo')] [string] $X) }", "X");

        Assert.Equal(new[] { "Foo" }, info.ParameterSets);
    }

    [Fact]
    public void Extract_ParameterSetsContainsUnionOfMultipleSetNames()
    {
        var script = "function F { param(" +
                     "[Parameter(ParameterSetName='A')] " +
                     "[Parameter(ParameterSetName='B')] " +
                     "[string] $X) }";
        var info = ExtractFrom(script, "X");

        Assert.Equal(new[] { "A", "B" }, info.ParameterSets);
    }

    [Fact]
    public void Extract_ParameterSetNameMatchIsCaseInsensitive()
    {
        var info = ExtractFrom("function F { param([Parameter(parameterSetname='Foo')] [string] $X) }", "X");

        Assert.Equal(new[] { "Foo" }, info.ParameterSets);
    }

    // ---- Aliases ------------------------------------------------------------

    [Fact]
    public void Extract_AliasesFromSingleAliasAttribute()
    {
        var info = ExtractFrom("function F { param([Alias('A')] [string] $X) }", "X");

        Assert.Equal(new[] { "A" }, info.Aliases);
    }

    [Fact]
    public void Extract_AliasesFromMultipleArgumentsInOneAliasAttribute()
    {
        var info = ExtractFrom("function F { param([Alias('A','B','C')] [string] $X) }", "X");

        Assert.Equal(new[] { "A", "B", "C" }, info.Aliases);
    }

    [Fact]
    public void Extract_AliasesEmpty_WhenNoAliasAttribute()
    {
        var info = ExtractFrom("function F { param([string] $X) }", "X");

        Assert.True(info.Aliases.IsDefaultOrEmpty);
    }

    // ---- ValidateSet --------------------------------------------------------

    [Fact]
    public void Extract_ValidateSetValuesFromSingleAttribute()
    {
        var info = ExtractFrom("function F { param([ValidateSet('A','B','C')] [string] $X) }", "X");

        Assert.Equal(new[] { "A", "B", "C" }, info.ValidateSetValues);
        Assert.True(info.HasValidateSet);
    }

    [Fact]
    public void Extract_ValidateSetValuesNull_WhenNoValidateSetAttribute()
    {
        var info = ExtractFrom("function F { param([string] $X) }", "X");

        Assert.Null(info.ValidateSetValues);
        Assert.False(info.HasValidateSet);
    }

    [Fact]
    public void Extract_ValidateSetValuesEmptyArray_WhenValidateSetPresentWithNoArguments()
    {
        // [ValidateSet()] (no arguments) is syntactically valid PowerShell. The attribute is
        // semantically a no-op, but the AST still contains it; the extractor must preserve that
        // presence so the orchestrator can distinguish "no enum constraint" from "closed enum
        // with zero options" per §13.3.
        var info = ExtractFrom("function F { param([ValidateSet()] [string] $X) }", "X");

        Assert.NotNull(info.ValidateSetValues);
        Assert.True(info.ValidateSetValues!.Value.IsDefaultOrEmpty);
        Assert.True(info.HasValidateSet);
    }

    // ---- ValidateRange ------------------------------------------------------

    [Fact]
    public void Extract_ValidateRangeMinAndMaxFromIntegerConstants()
    {
        var info = ExtractFrom("function F { param([ValidateRange(0,100)] [int] $X) }", "X");

        Assert.Equal("0", info.ValidateRangeMin);
        Assert.Equal("100", info.ValidateRangeMax);
        Assert.True(info.HasValidateRange);
    }

    [Fact]
    public void Extract_ValidateRangeMinOnlyWhenOneArgument()
    {
        var info = ExtractFrom("function F { param([ValidateRange(0)] [int] $X) }", "X");

        Assert.Equal("0", info.ValidateRangeMin);
        Assert.Null(info.ValidateRangeMax);
        Assert.True(info.HasValidateRange);
    }

    [Fact]
    public void Extract_ValidateRangeHandlesNegativeIntegers()
    {
        var info = ExtractFrom("function F { param([ValidateRange(-10,10)] [int] $X) }", "X");

        Assert.Equal("-10", info.ValidateRangeMin);
        Assert.Equal("10", info.ValidateRangeMax);
    }

    [Fact]
    public void Extract_ValidateRangeAbsentWhenNoAttribute()
    {
        var info = ExtractFrom("function F { param([string] $X) }", "X");

        Assert.Null(info.ValidateRangeMin);
        Assert.Null(info.ValidateRangeMax);
        Assert.False(info.HasValidateRange);
    }

    [Fact]
    public void Extract_ValidateRangeSkipped_WhenArgumentsAreStringConstants()
    {
        // [ValidateRange('1','10')] is syntactically valid PowerShell, but the arguments are
        // string constants, not numeric. The extractor preserves numeric values verbatim; non-
        // numeric arguments are skipped, both bounds stay null. Per §13.3 the schema emitter
        // surfaces the omission.
        var info = ExtractFrom("function F { param([ValidateRange('1','10')] [string] $X) }", "X");

        Assert.Null(info.ValidateRangeMin);
        Assert.Null(info.ValidateRangeMax);
        Assert.False(info.HasValidateRange);
    }

    [Fact]
    public void Extract_ValidateRangeAcceptsDoubleConstants()
    {
        // PowerShell parses 0.5 and 1.5 as System.Double. The previous int-only API silently
        // dropped these; the string-preserving API keeps them.
        var info = ExtractFrom("function F { param([ValidateRange(0.5, 1.5)] [double] $X) }", "X");

        Assert.Equal("0.5", info.ValidateRangeMin);
        Assert.Equal("1.5", info.ValidateRangeMax);
        Assert.True(info.HasValidateRange);
    }

    [Fact]
    public void Extract_ValidateRangeAcceptsNegativeDoubles()
    {
        var info = ExtractFrom("function F { param([ValidateRange(-0.5, 0.5)] [double] $X) }", "X");

        Assert.Equal("-0.5", info.ValidateRangeMin);
        Assert.Equal("0.5", info.ValidateRangeMax);
    }

    [Fact]
    public void Extract_ValidateRangeAcceptsLargeIntegerBeyondIntRange()
    {
        // 2147483648 = int.MaxValue + 1. PowerShell parses this literal as Int64 (it does not
        // fit in Int32). The string API must preserve the exact decimal form; coercing through
        // int would overflow.
        var info = ExtractFrom("function F { param([ValidateRange(2147483648, 9999999999)] [long] $X) }", "X");

        Assert.Equal("2147483648", info.ValidateRangeMin);
        Assert.Equal("9999999999", info.ValidateRangeMax);
    }

    [Fact]
    public void Extract_ValidateRangeAcceptsMixedIntegerAndDouble()
    {
        // First argument is an Int32, second is a Double. Both must be preserved.
        var info = ExtractFrom("function F { param([ValidateRange(0, 1.5)] [double] $X) }", "X");

        Assert.Equal("0", info.ValidateRangeMin);
        Assert.Equal("1.5", info.ValidateRangeMax);
    }

    [Fact]
    public void Extract_ValidateRangeSkipped_WhenArgumentsAreBooleanConstants()
    {
        // [ValidateRange($true, $false)] is syntactically valid PowerShell. Booleans are not
        // numeric, so both bounds stay null.
        var info = ExtractFrom("function F { param([ValidateRange($true, $false)] [string] $X) }", "X");

        Assert.Null(info.ValidateRangeMin);
        Assert.Null(info.ValidateRangeMax);
        Assert.False(info.HasValidateRange);
    }

    // ---- ValidatePattern ----------------------------------------------------

    [Fact]
    public void Extract_ValidatePatternFromStringConstant()
    {
        var info = ExtractFrom(@"function F { param([ValidatePattern('^\d+$')] [string] $X) }", "X");

        Assert.Equal(@"^\d+$", info.ValidatePattern);
        Assert.True(info.HasValidatePattern);
    }

    [Fact]
    public void Extract_ValidatePatternNullWhenAbsent()
    {
        var info = ExtractFrom("function F { param([string] $X) }", "X");

        Assert.Null(info.ValidatePattern);
        Assert.False(info.HasValidatePattern);
    }

    // ---- AllowNull / AllowEmptyString / AllowEmptyCollection ---------------

    [Fact]
    public void Extract_AllowFlagsAllFalse_WhenNoAllowAttributes()
    {
        var info = ExtractFrom("function F { param([string] $X) }", "X");

        Assert.False(info.AllowNull);
        Assert.False(info.AllowEmptyString);
        Assert.False(info.AllowEmptyCollection);
    }

    [Fact]
    public void Extract_AllowFlagsAllTrue_WhenAllThreeAttributesPresent()
    {
        var script = "function F { param(" +
                     "[AllowNull()] " +
                     "[AllowEmptyString()] " +
                     "[AllowEmptyCollection()] " +
                     "[string] $X) }";
        var info = ExtractFrom(script, "X");

        Assert.True(info.AllowNull);
        Assert.True(info.AllowEmptyString);
        Assert.True(info.AllowEmptyCollection);
    }

    [Theory]
    [InlineData("[AllowNull()]", true, false, false)]
    [InlineData("[AllowEmptyString()]", false, true, false)]
    [InlineData("[AllowEmptyCollection()]", false, false, true)]
    public void Extract_AllowFlagsResolveIndividually(string attribute, bool allowNull, bool allowEmptyString, bool allowEmptyCollection)
    {
        var script = $"function F {{ param({attribute} [string] $X) }}";
        var info = ExtractFrom(script, "X");

        Assert.Equal(allowNull, info.AllowNull);
        Assert.Equal(allowEmptyString, info.AllowEmptyString);
        Assert.Equal(allowEmptyCollection, info.AllowEmptyCollection);
    }

    // ---- Case-insensitive attribute matching -------------------------------

    [Fact]
    public void Extract_AttributeNameMatchIsCaseInsensitive()
    {
        // PowerShell resolves attributes case-insensitively; the extractor must do the same.
        var script = "function F { param(" +
                     "[parameter(mandatory)] " +
                     "[ALIAS('A')] " +
                     "[validateSet('X')] " +
                     "[VALIDATERANGE(0,1)] " +
                     "[validatepattern('^a$')] " +
                     "[allowNull()] " +
                     "[string] $X) }";
        var info = ExtractFrom(script, "X");

        Assert.True(info.IsMandatory);
        Assert.Equal(new[] { "A" }, info.Aliases);
        Assert.Equal(new[] { "X" }, info.ValidateSetValues);
        Assert.Equal("0", info.ValidateRangeMin);
        Assert.Equal("1", info.ValidateRangeMax);
        Assert.Equal("^a$", info.ValidatePattern);
        Assert.True(info.AllowNull);
    }

    [Fact]
    public void Extract_AttributeNameMatches_WhenAttributeSuffixIsExplicit()
    {
        // PowerShell's attribute resolution tolerates an explicit "Attribute" suffix on the
        // class name. The extractor must canonicalize [AliasAttribute] to [Alias],
        // [ParameterAttribute] to [Parameter], and so on.
        var script = "function F { param(" +
                     "[ParameterAttribute(Mandatory)] " +
                     "[AliasAttribute('A')] " +
                     "[ValidateSetAttribute('X','Y')] " +
                     "[ValidateRangeAttribute(0, 10)] " +
                     "[ValidatePatternAttribute('^a$')] " +
                     "[AllowNullAttribute()] " +
                     "[string] $X) }";
        var info = ExtractFrom(script, "X");

        Assert.True(info.IsMandatory);
        Assert.Equal(new[] { "A" }, info.Aliases);
        Assert.Equal(new[] { "X", "Y" }, info.ValidateSetValues);
        Assert.Equal("0", info.ValidateRangeMin);
        Assert.Equal("10", info.ValidateRangeMax);
        Assert.Equal("^a$", info.ValidatePattern);
        Assert.True(info.AllowNull);
    }

    [Fact]
    public void Extract_AttributeNameMatches_FullyQualifiedAttributeType()
    {
        // PowerShell's attribute resolution also tolerates the fully qualified CLR type name.
        // The extractor must canonicalize [System.Management.Automation.AliasAttribute] to
        // [Alias], and likewise for the other supported families.
        var script = "function F { param(" +
                     "[System.Management.Automation.ParameterAttribute(Mandatory)] " +
                     "[System.Management.Automation.AliasAttribute('A')] " +
                     "[System.Management.Automation.ValidateSetAttribute('X','Y')] " +
                     "[System.Management.Automation.ValidateRangeAttribute(0, 10)] " +
                     "[System.Management.Automation.ValidatePatternAttribute('^a$')] " +
                     "[System.Management.Automation.AllowNullAttribute()] " +
                     "[string] $X) }";
        var info = ExtractFrom(script, "X");

        Assert.True(info.IsMandatory);
        Assert.Equal(new[] { "A" }, info.Aliases);
        Assert.Equal(new[] { "X", "Y" }, info.ValidateSetValues);
        Assert.Equal("0", info.ValidateRangeMin);
        Assert.Equal("10", info.ValidateRangeMax);
        Assert.Equal("^a$", info.ValidatePattern);
        Assert.True(info.AllowNull);
    }

    // ---- Integration: realistic Get-Foo shape -------------------------------

    [Fact]
    public void Extract_FullIntegration_RealisticGetFooParameter()
    {
        // Mirrors a typical "mandatory string with enum constraint and an alias" production parameter.
        var script = "function Get-Foo {\n" +
                     "    param(\n" +
                     "        [Parameter(Mandatory)]\n" +
                     "        [Alias('N','Nm')]\n" +
                     "        [ValidateSet('A','B','C')]\n" +
                     "        [ValidateRange(1, 99)]\n" +
                     "        [string] $Name\n" +
                     "    )\n" +
                     "}\n";
        var info = ExtractFrom(script, "Name");

        Assert.Equal("string", info.Type);
        Assert.True(info.IsMandatory);
        Assert.True(info.ParameterSets.IsDefaultOrEmpty);
        Assert.Equal(new[] { "N", "Nm" }, info.Aliases);
        Assert.Equal(new[] { "A", "B", "C" }, info.ValidateSetValues);
        Assert.True(info.HasValidateSet);
        Assert.Equal("1", info.ValidateRangeMin);
        Assert.Equal("99", info.ValidateRangeMax);
        Assert.True(info.HasValidateRange);
        Assert.Null(info.ValidatePattern);
        Assert.False(info.HasValidatePattern);
        Assert.False(info.AllowNull);
        Assert.False(info.AllowEmptyString);
        Assert.False(info.AllowEmptyCollection);
    }

    // ---- Helpers ------------------------------------------------------------

    private static ParameterAttributeInfo ExtractFrom(string script, string parameterName)
    {
        var parameter = ParseParameter(script, parameterName);
        return ParameterAttributeExtractor.Extract(parameter);
    }

    private static ParameterAst ParseParameter(string script, string parameterName)
    {
        var ast = Parser.ParseInput(script, out _, out var errors);
        Assert.Empty(errors);
        var function = ast.FindAll(a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true)
            .Cast<FunctionDefinitionAst>()
            .Single();
        var paramBlock = function.Body.ParamBlock
            ?? throw new InvalidOperationException("Test fixture has no param() block.");
        return paramBlock.Parameters.Single(p => p.Name.VariablePath.UserPath == parameterName);
    }
}

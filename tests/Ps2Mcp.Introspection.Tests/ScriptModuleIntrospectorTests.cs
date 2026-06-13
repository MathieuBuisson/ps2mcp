using System;
using System.IO;
using System.Linq;
using Ps2Mcp.Core;
using Ps2Mcp.Introspection;

namespace Ps2Mcp.Introspection.Tests;

public sealed class ScriptModuleIntrospectorTests : IDisposable
{
    private readonly string _tempDir;

    public ScriptModuleIntrospectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ps2mcp-introspector-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Introspect_ThrowsArgumentNullException_ForNullParseResult()
    {
        Assert.Throws<ArgumentNullException>(() => ScriptModuleIntrospector.Introspect(null!));
    }

    [Fact]
    public void Introspect_DerivesModuleNameFromFilePath()
    {
        var path = WritePsm1("DiverseParams.psm1", "function Get-Foo { [CmdletBinding()] param() }");

        var result = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path));

        Assert.Equal("DiverseParams", result.Module.Name);
        Assert.Null(result.Module.Version);
    }

    [Fact]
    public void Introspect_ProducesOneToolPerTopLevelFunction()
    {
        var path = WritePsm1("TestModule.psm1",
            """
            function Get-Foo { [CmdletBinding()] param() }
            function Set-Bar { [CmdletBinding()] param() }
            """);

        var result = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path));

        Assert.Equal(2, result.Tools.Length);
        Assert.Contains(result.Tools, t => t.ToolName == "Get-Foo");
        Assert.Contains(result.Tools, t => t.ToolName == "Set-Bar");
    }

    [Fact]
    public void Introspect_ToolNameAndSourceCommandMatchFunctionName()
    {
        var path = WritePsm1("TestModule.psm1",
            "function Get-Foo { [CmdletBinding()] param() }");

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        Assert.Equal("Get-Foo", tool.ToolName);
        Assert.Equal("Get-Foo", tool.SourceCommand);
    }

    [Fact]
    public void Introspect_ToolDescriptionFromHelpSynopsis()
    {
        var path = WritePsm1("TestModule.psm1",
            """
            function Get-Foo {
            <#
            .SYNOPSIS
            Gets a foo.
            .DESCRIPTION
            Returns a foo object.
            #>
            [CmdletBinding()]
            param()
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        Assert.Equal("Gets a foo.", tool.Description);
    }

    [Fact]
    public void Introspect_ToolDescriptionFallsBackToHelpDescriptionWhenSynopsisAbsent()
    {
        var path = WritePsm1("TestModule.psm1",
            """
            function Get-Foo {
            <#
            .DESCRIPTION
            Returns a foo object.
            #>
            [CmdletBinding()]
            param()
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        Assert.Equal("Returns a foo object.", tool.Description);
    }

    [Fact]
    public void Introspect_ToolCapturesDiverseParameterShapes()
    {
        // Fixture content is embedded in the test assembly and materialized to a temp .psm1 here:
        // 2 functions covering all 8 required parameter shapes — mandatory, optional, enum
        // (ValidateSet), range (ValidateRange), pattern (ValidatePattern), array, switch, and
        // secure-string. See ParameterShapeCoverage below for the per-parameter expectations.
        var path = WriteFixture(
            "DiverseParamsModule.psm1",
            FixtureResourceLoader.LoadUtf8Text(FixtureResourceLoader.DiverseParamsModule));

        var result = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path));
        Assert.Equal("DiverseParamsModule", result.Module.Name);
        Assert.Equal(2, result.Tools.Length);

        var getServer = result.Tools.Single(t => t.ToolName == "Get-Server");
        var setConfig = result.Tools.Single(t => t.ToolName == "Set-Config");

        // -- Get-Server: mandatory, optional, enum, array --
        Assert.Equal(3, getServer.Parameters.Length);
        Assert.Equal("Gets a server.", getServer.Description);

        var name = getServer.Parameters.Single(p => p.Name == "Name");
        Assert.True(name.IsMandatory);
        Assert.Equal("string", name.Type);
        Assert.False(name.IsSecure);
        Assert.Equal("The server name.", name.Description);
        Assert.Null(name.DefaultValue);

        var environment = getServer.Parameters.Single(p => p.Name == "Environment");
        Assert.False(environment.IsMandatory);
        Assert.Equal("string", environment.Type);
        Assert.Equal("prod", environment.DefaultValue);

        var tags = getServer.Parameters.Single(p => p.Name == "Tags");
        Assert.False(tags.IsMandatory);
        Assert.Equal("string[]", tags.Type);

        // Get-Server schema: object with one property per parameter; only mandatory params are Required.
        Assert.Equal("object", getServer.Schema.Type);
        Assert.Equal(3, getServer.Schema.Properties.Length);
        Assert.Equal("Name", Assert.Single(getServer.Schema.Required));
        Assert.DoesNotContain("Environment", getServer.Schema.Required);
        Assert.DoesNotContain("Tags", getServer.Schema.Required);

        var nameSchema = getServer.Schema.Properties.Single(p => p.Name == "Name");
        Assert.Equal("string", nameSchema.Type);
        Assert.Null(nameSchema.Enum);
        Assert.Null(nameSchema.Minimum);
        Assert.Null(nameSchema.Maximum);
        Assert.Null(nameSchema.Pattern);

        var environmentSchema = getServer.Schema.Properties.Single(p => p.Name == "Environment");
        Assert.Equal("string", environmentSchema.Type);
        Assert.Equal(new[] { "prod", "staging", "dev" }, environmentSchema.Enum?.ToArray());
        Assert.Null(environmentSchema.Minimum);
        Assert.Null(environmentSchema.Maximum);
        Assert.Null(environmentSchema.Pattern);

        var tagsSchema = getServer.Schema.Properties.Single(p => p.Name == "Tags");
        Assert.Equal("array", tagsSchema.Type);
        Assert.NotNull(tagsSchema.Schema);
        Assert.Equal("array", tagsSchema.Schema!.Type);
        Assert.NotNull(tagsSchema.Schema.Items);
        Assert.Equal("string", tagsSchema.Schema.Items!.Type);

        // -- Set-Config: mandatory, range, optional, pattern, switch, secure-string --
        Assert.Equal(4, setConfig.Parameters.Length);

        var maxRetries = setConfig.Parameters.Single(p => p.Name == "MaxRetries");
        Assert.True(maxRetries.IsMandatory);
        Assert.Equal("int", maxRetries.Type);
        Assert.False(maxRetries.IsSecure);

        var prefix = setConfig.Parameters.Single(p => p.Name == "Prefix");
        Assert.False(prefix.IsMandatory);
        Assert.Equal("string", prefix.Type);
        Assert.Equal("Default", prefix.DefaultValue);

        var force = setConfig.Parameters.Single(p => p.Name == "Force");
        Assert.False(force.IsMandatory);
        Assert.Equal("switch", force.Type);

        var password = setConfig.Parameters.Single(p => p.Name == "Password");
        Assert.False(password.IsMandatory);
        Assert.Equal("SecureString", password.Type);
        Assert.True(password.IsSecure);

        // Set-Config schema: ValidateRange and ValidatePattern flow into Minimum/Maximum/Pattern.
        var maxRetriesSchema = setConfig.Schema.Properties.Single(p => p.Name == "MaxRetries");
        Assert.Equal("integer", maxRetriesSchema.Type);
        Assert.Equal("1", maxRetriesSchema.Minimum);
        Assert.Equal("100", maxRetriesSchema.Maximum);
        Assert.Null(maxRetriesSchema.Enum);
        Assert.Null(maxRetriesSchema.Pattern);

        var prefixSchema = setConfig.Schema.Properties.Single(p => p.Name == "Prefix");
        Assert.Equal("^[A-Z][a-zA-Z0-9]*$", prefixSchema.Pattern);
        Assert.Null(prefixSchema.Enum);
        Assert.Null(prefixSchema.Minimum);
        Assert.Null(prefixSchema.Maximum);

        var forceSchema = setConfig.Schema.Properties.Single(p => p.Name == "Force");
        Assert.Equal("boolean", forceSchema.Type);

        var passwordSchema = setConfig.Schema.Properties.Single(p => p.Name == "Password");
        Assert.Equal("SecureString", passwordSchema.Type);

        Assert.Contains("MaxRetries", setConfig.Schema.Required);
        Assert.DoesNotContain("Prefix", setConfig.Schema.Required);
        Assert.DoesNotContain("Force", setConfig.Schema.Required);
        Assert.DoesNotContain("Password", setConfig.Schema.Required);
    }

    [Theory]
    // PowerShell type names are case-insensitive: [securestring] and [pscredential] are valid
    // declarations that must be detected as secure. The negative case guards against false
    // positives (a non-secure type must never be marked IsSecure, regardless of casing).
    [InlineData("SecureString", true)]
    [InlineData("securestring", true)]
    [InlineData("SECURESTRING", true)]
    [InlineData("PSCredential", true)]
    [InlineData("pscredential", true)]
    [InlineData("PSCREDENTIAL", true)]
    [InlineData("string", false)]
    [InlineData("String", false)]
    [InlineData("int", false)]
    public void Introspect_IsSecureDetectionIsCaseInsensitive(string typeText, bool expectedIsSecure)
    {
        var path = WritePsm1("SecureTypeCase.psm1",
            "function Test-Func { [CmdletBinding()] param([Parameter(Mandatory)][" + typeText + "]$Value) }");

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        Assert.Equal(expectedIsSecure, Assert.Single(tool.Parameters).IsSecure);
    }

    [Fact]
    public void Introspect_ToolCapturesOutputTypeAttribute()
    {
        var path = WritePsm1("TestModule.psm1",
            """
            function Get-Foo {
            [CmdletBinding()]
            [OutputType('FooResult')]
            param()
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        Assert.NotNull(tool.Output);
        Assert.Equal("FooResult", tool.Output!.OutputTypeName);
    }

    [Fact]
    public void Introspect_ToolCapturesOutputTypeAttributeNamedTypeNameArgument()
    {
        var path = WritePsm1("TestModule.psm1",
            """
            function Get-Foo {
            [CmdletBinding()]
            [OutputType(TypeName = 'FooResult')]
            param()
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        Assert.NotNull(tool.Output);
        Assert.Equal("FooResult", tool.Output!.OutputTypeName);
    }

    [Fact]
    public void Introspect_MultipleOutputTypeAttributes_PreservesFirstDeclaredType()
    {
        var path = WritePsm1("TestModule.psm1",
            """
            function Get-Foo {
            [CmdletBinding()]
            [OutputType('FirstResult')]
            [OutputType(TypeName = 'SecondResult')]
            param()
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        Assert.NotNull(tool.Output);
        Assert.Equal("FirstResult", tool.Output!.OutputTypeName);
    }

    [Fact]
    public void Introspect_ToolOutputTypeIsNullWhenAttributeAbsent()
    {
        var path = WritePsm1("TestModule.psm1",
            "function Get-Foo { [CmdletBinding()] param() }");

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        Assert.Null(tool.Output);
    }

    [Fact]
    public void Introspect_ToolCapturesHelpExamples()
    {
        var path = WritePsm1("TestModule.psm1",
            """
            function Get-Foo {
            <#
            .SYNOPSIS
            Gets a foo.
            .EXAMPLE
            Get-Foo -Name 'bar'
            #>
            [CmdletBinding()]
            param([string]$Name)
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        Assert.NotNull(tool.Help);
        Assert.Equal("Gets a foo.", tool.Help!.Synopsis);
        Assert.Single(tool.Help.Examples);
        Assert.Contains("Get-Foo -Name 'bar'", tool.Help.Examples[0].Code);
    }

    [Fact]
    public void Introspect_ExcludesNestedFunctions()
    {
        // Inner is a private closure inside Outer; only the top-level Outer should be exposed.
        var path = WritePsm1("TestModule.psm1",
            """
            function Outer {
            [CmdletBinding()]
            param()
            function Inner { 'x' }
            }
            """);

        var result = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path));

        Assert.Single(result.Tools);
        Assert.Equal("Outer", result.Tools[0].ToolName);
        Assert.DoesNotContain(result.Tools, t => t.ToolName == "Inner");
    }

    [Fact]
    public void Introspect_EmptyModuleProducesNoTools()
    {
        var path = WritePsm1("TestModule.psm1", string.Empty);

        var result = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path));

        Assert.Empty(result.Tools);
        Assert.Equal("TestModule", result.Module.Name);
    }

    [Fact]
    public void Introspect_FunctionWithNoParamBlockProducesEmptyParameterList()
    {
        var path = WritePsm1("TestModule.psm1",
            "function Get-Foo { 'literal' }");

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        Assert.Empty(tool.Parameters);
        Assert.Equal("object", tool.Schema.Type);
        Assert.Empty(tool.Schema.Properties);
        Assert.Empty(tool.Schema.Required);
    }

    [Fact]
    public void Introspect_EmptyFunctionBodyProducesToolWithMinimalFields()
    {
        // An empty body exercises the defensive null-handling paths: function.Body is
        // non-null (the parser always creates a ScriptBlockAst) but its ParamBlock is null,
        // its named blocks are null, and its Statements collection is empty. The introspector
        // must produce a tool with empty parameters, an "object" schema with no properties,
        // no help, and no output type — not throw.
        var path = WritePsm1("TestModule.psm1", "function F { }");

        var result = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path));

        var tool = Assert.Single(result.Tools);
        Assert.Equal("F", tool.ToolName);
        Assert.Equal("F", tool.SourceCommand);
        Assert.Equal(string.Empty, tool.Description);
        Assert.Empty(tool.Parameters);
        Assert.Null(tool.RequiredParameterSet);
        Assert.Equal("object", tool.Schema.Type);
        Assert.Empty(tool.Schema.Properties);
        Assert.Empty(tool.Schema.Required);
        Assert.Null(tool.Help);
        Assert.Null(tool.Output);
    }

    [Fact]
    public void Introspect_DefaultValueFromArrayLiteral()
    {
        // Populated array literal: the source text preserves the elements and quoting.
        // The PowerShell parser uses ArrayExpressionAst (the comma-operator AST) for
        // both the explicit @(...) form and the bare `,`-list form, so we surface the
        // raw Extent.Text from either AST type.
        var populatedPath = WritePsm1("PopulatedArray.psm1",
            "function Get-Foo { [CmdletBinding()] param([string[]]$Tags = @('a', 'b')) }");

        var populatedTool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(populatedPath)).Tools[0];

        Assert.Equal("@('a', 'b')", Assert.Single(populatedTool.Parameters).DefaultValue);

        // Empty array literal: the parser still emits an ArrayExpressionAst with zero
        // children, so Extent.Text returns "@()".
        var emptyPath = WritePsm1("EmptyArray.psm1",
            "function Get-Foo { [CmdletBinding()] param([string[]]$Tags = @()) }");

        var emptyTool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(emptyPath)).Tools[0];

        Assert.Equal("@()", Assert.Single(emptyTool.Parameters).DefaultValue);
    }

    [Fact]
    public void Introspect_DefaultValueFromVariableReference()
    {
        // Variable references are surfaced as the variable name (without the leading $) so
        // the schema emitter can decide whether to bind or skip the default.
        var path = WritePsm1("VarRef.psm1",
            "function Get-Foo { [CmdletBinding()] param([string]$Name = $someVariable) }");

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        Assert.Equal("someVariable", Assert.Single(tool.Parameters).DefaultValue);
    }

    [Fact]
    public void Introspect_ProducesIrVersionCurrent()
    {
        var path = WritePsm1("TestModule.psm1",
            "function Get-Foo { [CmdletBinding()] param() }");

        var result = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path));

        Assert.Equal(IrVersion.Current, result.IrVersion);
    }

    private string WritePsm1(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private string WriteFixture(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}

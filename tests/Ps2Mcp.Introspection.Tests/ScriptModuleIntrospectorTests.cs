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
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; xUnit will clean up the temp path on process exit.
        }
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

    private string LoadDiverseParamsModule() =>
        FixtureResourceLoader.LoadUtf8Text(FixtureResourceLoader.DiverseParamsModule);

    [Fact]
    public void Introspect_DiverseModule_ProducesExpectedTools()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());

        var result = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path));

        Assert.Equal("DiverseParamsModule", result.Module.Name);
        Assert.Equal(2, result.Tools.Length);
        Assert.Contains(result.Tools, t => t.ToolName == "Get-Server");
        Assert.Contains(result.Tools, t => t.ToolName == "Set-Config");
    }

    [Fact]
    public void Introspect_ToolDescription_FromHelpSynopsis()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var getServer = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Get-Server");

        Assert.Equal("Gets a server.", getServer.Description);
    }

    [Fact]
    public void Introspect_MandatoryParameter_IsRequiredInSchema()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var getServer = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Get-Server");

        var name = getServer.Parameters.Single(p => p.Name == "Name");
        Assert.True(name.IsMandatory);
        Assert.Contains("Name", getServer.Schema.Required);
    }

    [Fact]
    public void Introspect_OptionalParameter_NotInRequiredList()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var getServer = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Get-Server");

        var environment = getServer.Parameters.Single(p => p.Name == "Environment");
        Assert.False(environment.IsMandatory);
        Assert.DoesNotContain("Environment", getServer.Schema.Required);
    }

    [Fact]
    public void Introspect_ParameterWithDefault_PreservesDefaultValue()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var getServer = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Get-Server");

        var environment = getServer.Parameters.Single(p => p.Name == "Environment");
        Assert.Equal("prod", environment.DefaultValue);
    }

    [Fact]
    public void Introspect_ParameterWithHelpDescription_PreservesDescription()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var getServer = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Get-Server");

        var name = getServer.Parameters.Single(p => p.Name == "Name");
        Assert.Equal("The server name.", name.Description);
    }

    [Fact]
    public void Introspect_ValidateSet_MapsToEnum()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var getServer = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Get-Server");

        var environmentSchema = getServer.Schema.Properties.Single(p => p.Name == "Environment");
        Assert.Equal("string", environmentSchema.Type);
        Assert.Equal(new[] { "prod", "staging", "dev" }, environmentSchema.Enum?.ToArray());
    }

    [Fact]
    public void Introspect_ArrayParameter_MapsToArraySchemaWithItems()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var getServer = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Get-Server");

        var tags = getServer.Parameters.Single(p => p.Name == "Tags");
        Assert.Equal("string[]", tags.Type);

        var tagsSchema = getServer.Schema.Properties.Single(p => p.Name == "Tags");
        Assert.Equal("array", tagsSchema.Type);
        Assert.NotNull(tagsSchema.Schema);
        Assert.NotNull(tagsSchema.Schema!.Items);
        Assert.Equal("string", tagsSchema.Schema.Items!.Type);
    }

    [Fact]
    public void Introspect_ValidateRange_MapsToBounds()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var setConfig = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Set-Config");

        var maxRetriesSchema = setConfig.Schema.Properties.Single(p => p.Name == "MaxRetries");
        Assert.Equal("integer", maxRetriesSchema.Type);
        Assert.Equal("1", maxRetriesSchema.Minimum);
        Assert.Equal("100", maxRetriesSchema.Maximum);
        Assert.Null(maxRetriesSchema.Enum);
        Assert.Null(maxRetriesSchema.Pattern);
    }

    [Fact]
    public void Introspect_ValidatePattern_MapsToPattern()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var setConfig = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Set-Config");

        var prefixSchema = setConfig.Schema.Properties.Single(p => p.Name == "Prefix");
        Assert.Equal("^[A-Z][a-zA-Z0-9]*$", prefixSchema.Pattern);
        Assert.Null(prefixSchema.Enum);
        Assert.Null(prefixSchema.Minimum);
        Assert.Null(prefixSchema.Maximum);
    }

    [Fact]
    public void Introspect_SwitchParameter_MapsToBoolean()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var setConfig = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Set-Config");

        var force = setConfig.Parameters.Single(p => p.Name == "Force");
        Assert.Equal("switch", force.Type);
        Assert.False(force.IsMandatory);

        var forceSchema = setConfig.Schema.Properties.Single(p => p.Name == "Force");
        Assert.Equal("boolean", forceSchema.Type);
    }

    [Fact]
    public void Introspect_SecureStringParameter_IsFlaggedSecure()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var setConfig = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Set-Config");

        var password = setConfig.Parameters.Single(p => p.Name == "Password");
        Assert.Equal("SecureString", password.Type);
        Assert.True(password.IsSecure);
        Assert.False(password.IsMandatory);

        var passwordSchema = setConfig.Schema.Properties.Single(p => p.Name == "Password");
        Assert.Equal("object", passwordSchema.Type);
        Assert.NotNull(passwordSchema.Schema);
        Assert.Equal("SecureString", passwordSchema.Schema!.ComplexType);
    }

    [Fact]
    public void Introspect_SetConfig_OnlyMandatoryInRequiredList()
    {
        var path = WritePsm1("DiverseParamsModule.psm1", LoadDiverseParamsModule());
        var setConfig = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path))
            .Tools.Single(t => t.ToolName == "Set-Config");

        Assert.Contains("MaxRetries", setConfig.Schema.Required);
        Assert.DoesNotContain("Prefix", setConfig.Schema.Required);
        Assert.DoesNotContain("Force", setConfig.Schema.Required);
        Assert.DoesNotContain("Password", setConfig.Schema.Required);
    }

    [Fact]
    public void Introspect_ComplexTypeParameter_SchemaMapsToObjectWithComplexTypeMarker()
    {
        var path = WritePsm1("ComplexType.psm1",
            """
            function Get-Foo {
                [CmdletBinding()]
                param([System.ServiceProcess.ServiceController]$Service)
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];
        var param = Assert.Single(tool.Parameters);
        var schema = tool.Schema.Properties.Single(p => p.Name == "Service");

        Assert.Equal("ServiceController", param.Type);
        Assert.Equal("object", schema.Type);
        Assert.NotNull(schema.Schema);
        Assert.Equal("ServiceController", schema.Schema!.ComplexType);
    }

    [Fact]
    public void Introspect_ComplexTypeArrayParameter_SchemaMapsToArrayWithObjectItems()
    {
        var path = WritePsm1("ComplexArray.psm1",
            """
            function Get-Foo {
                [CmdletBinding()]
                param([System.ServiceProcess.ServiceController[]]$Services)
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];
        var schema = tool.Schema.Properties.Single(p => p.Name == "Services");

        Assert.Equal("array", schema.Type);
        Assert.NotNull(schema.Schema);
        Assert.NotNull(schema.Schema!.Items);
        Assert.Equal("object", schema.Schema.Items!.Type);
        Assert.Equal("ServiceController", schema.Schema.Items!.ComplexType);
    }

    [Fact]
    public void Introspect_ComplexTypeParameter_NoSpeculativeTyping()
    {
        // Unknown types must always map to "object" — never to the raw type name.
        // This guards against speculative typing where the mapper guesses a schema type
        // that doesn't correspond to a JSON Schema primitive.
        var path = WritePsm1("Speculative.psm1",
            """
            function Get-Foo {
                [CmdletBinding()]
                param(
                    [System.ServiceProcess.ServiceController]$Service,
                    [MyApp.Foo.Bar]$Custom,
                    [Nullable[int]]$Nullable
                )
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];

        foreach (var prop in tool.Schema.Properties)
        {
            Assert.Equal("object", prop.Type);
            Assert.NotNull(prop.Schema);
            Assert.NotNull(prop.Schema!.ComplexType);
        }
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

    [Fact]
    public void Introspect_EmptyValidateSet_ProducesEmptyEnumArray()
    {
        var path = WritePsm1("EmptySet.psm1",
            """
            function Get-Foo {
                [CmdletBinding()]
                param([ValidateSet()] [string]$Choice)
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];
        var schema = tool.Schema.Properties.Single(p => p.Name == "Choice");

        Assert.Equal("string", schema.Type);
        Assert.NotNull(schema.Enum);
        Assert.Empty(schema.Enum!.Value);
    }

    [Fact]
    public void Introspect_InvertedValidateRange_PreservesBoundsAsStrings()
    {
        // PowerShell allows min > max (inverted range); the extractor captures both
        // bounds without validation. The schema emitter decides how to handle it.
        var path = WritePsm1("InvertedRange.psm1",
            """
            function Get-Foo {
                [CmdletBinding()]
                param([ValidateRange(100, 1)] [int]$Value)
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];
        var schema = tool.Schema.Properties.Single(p => p.Name == "Value");

        Assert.Equal("integer", schema.Type);
        Assert.Equal("100", schema.Minimum);
        Assert.Equal("1", schema.Maximum);
    }

    [Fact]
    public void Introspect_InvalidRegexPattern_PreservesPatternString()
    {
        // PowerShell tolerates many regex patterns at parse time; the extractor
        // captures the string verbatim without compiling or validating it.
        var path = WritePsm1("InvalidRegex.psm1",
            """
            function Get-Foo {
                [CmdletBinding()]
                param([ValidatePattern("(unclosed")] [string]$Text)
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];
        var schema = tool.Schema.Properties.Single(p => p.Name == "Text");

        Assert.Equal("string", schema.Type);
        Assert.Equal("(unclosed", schema.Pattern);
    }

    [Fact]
    public void Introspect_MultipleValidatorsOnSameParameter_AllConstraintsPreserved()
    {
        var path = WritePsm1("MultiValidator.psm1",
            """
            function Get-Foo {
                [CmdletBinding()]
                param(
                    [Parameter(Mandatory)]
                    [ValidateSet('a', 'b', 'c')]
                    [ValidateRange(1, 10)]
                    [ValidatePattern("^[a-c]$")]
                    [string]$Value
                )
            }
            """);

        var tool = ScriptModuleIntrospector.Introspect(ScriptModuleParser.Parse(path)).Tools[0];
        var schema = tool.Schema.Properties.Single(p => p.Name == "Value");

        Assert.Equal("string", schema.Type);
        Assert.Equal(new[] { "a", "b", "c" }, schema.Enum?.ToArray());
        Assert.Equal("1", schema.Minimum);
        Assert.Equal("10", schema.Maximum);
        Assert.Equal("^[a-c]$", schema.Pattern);
        Assert.Contains("Value", tool.Schema.Required);
    }

    private string WritePsm1(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}

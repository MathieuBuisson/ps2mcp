using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ps2Mcp.Core;
using Xunit;

namespace Ps2Mcp.Introspection.Tests;

// Records a real pwsh Introspection.ps1 run against a binary module and asserts
// the mapper produces the expected IR. The fixture is committed at
// tests/Ps2Mcp.Introspection.Tests/Fixtures/binary-metadata-microsoft-powershell-management.json;
// regenerate by running Introspection.ps1 against a built-in PowerShell module
// (e.g. Microsoft.PowerShell.Management).
public class CommandMetadataMapperTests
{
    private static readonly Lazy<BinaryIntrospectionPayload> RealFixture = new(LoadFixtureCore);

    [Fact]
    public void Map_RealMicrosoftPowerShellManagementPayload_ProducesServerDefinition()
    {
        var payload = LoadFixture();

        var result = CommandMetadataMapper.Map(payload);

        Assert.Equal("Microsoft.PowerShell.Management", result.Module.Name);
        Assert.Null(result.Module.Version);
        Assert.True(result.Tools.Length > 0,
            "Microsoft.PowerShell.Management should expose at least one command.");
        Assert.NotEqual(0, result.IrVersion);
    }

    [Fact]
    public void Map_RealPayload_AllCommandNamesArePreserved()
    {
        var payload = LoadFixture();

        var result = CommandMetadataMapper.Map(payload);

        var expectedNames = payload.Commands.Select(c => c.Name).ToArray();
        var actualNames = result.Tools.Select(t => t.ToolName).ToArray();
        Assert.Equal(expectedNames, actualNames);
    }

    [Fact]
    public void Map_RealPayload_AllCommandSourceCommandEqualsToolName()
    {
        var payload = LoadFixture();

        var result = CommandMetadataMapper.Map(payload);

        Assert.All(result.Tools, t => Assert.Equal(t.ToolName, t.SourceCommand));
    }

    [Fact]
    public void Map_RealPayload_AllToolsUseExecutionDepthFour()
    {
        var payload = LoadFixture();

        var result = CommandMetadataMapper.Map(payload);

        Assert.All(result.Tools, t => Assert.Equal(4, t.Execution.SerializationDepth));
    }

    [Fact]
    public void Map_RealPayload_HelpIsNullAndDescriptionIsEmpty()
    {
        // The binary payload does not carry comment-based help or the cmdlet's
        // synopsis. Both fields are intentionally null/empty so the schema
        // mapper can fill them in later from the cmdlet's Get-Help output.
        var payload = LoadFixture();

        var result = CommandMetadataMapper.Map(payload);

        Assert.All(result.Tools, t =>
        {
            Assert.Null(t.Help);
            Assert.Equal(string.Empty, t.Description);
        });
    }

    [Fact]
    public void Map_RealPayload_ParameterNamesPreservedOnKnownCommand()
    {
        // Get-Process is a stable, well-known cmdlet. Its parameters form a
        // contract — if the mapper drops or renames one, the test fails loudly.
        var payload = LoadFixture();

        var result = CommandMetadataMapper.Map(payload);
        var getProcess = result.Tools.SingleOrDefault(t => t.ToolName == "Get-Process");
        Assert.NotNull(getProcess);
        var paramNames = getProcess.Parameters.Select(p => p.Name).ToImmutableArray();
        Assert.Contains("Name", paramNames);
        Assert.Contains("Id", paramNames);
        Assert.Contains("InputObject", paramNames);
    }

    [Fact]
    public void Map_RealPayload_ParameterTypesAreHumanized()
    {
        // TypeNameHumanizer should strip the System. prefix on common types;
        // mapping must not return the raw FullName.
        var payload = LoadFixture();

        var result = CommandMetadataMapper.Map(payload);
        var getProcess = result.Tools.Single(t => t.ToolName == "Get-Process");
        Assert.All(getProcess.Parameters, p =>
        {
            Assert.DoesNotContain("System.", p.Type);
        });
    }

    [Fact]
    public void Map_RealPayload_IsMandatoryPreservedFromPayload()
    {
        // The mapper does not invent IsMandatory; the boolean from the JSON
        // payload flows through to the IR unchanged.
        var payload = LoadFixture();

        var result = CommandMetadataMapper.Map(payload);
        var getProcess = result.Tools.Single(t => t.ToolName == "Get-Process");
        var nameParam = getProcess.Parameters.Single(p => p.Name == "Name");
        var expected = payload.Commands
            .Single(c => c.Name == "Get-Process")
            .Parameters.Single(p => p.Name == "Name")
            .IsMandatory;
        Assert.Equal(expected, nameParam.IsMandatory);
    }

    [Fact]
    public void Map_RealPayload_RequiredParameterSetFromDefaultParameterSetName()
    {
        var payload = LoadFixture();

        var result = CommandMetadataMapper.Map(payload);

        // The first payload command's RequiredParameterSet should equal its
        // DefaultParameterSetName (possibly empty / null after the mapping).
        for (var i = 0; i < result.Tools.Length; i++)
        {
            var expected = string.IsNullOrEmpty(payload.Commands[i].DefaultParameterSetName)
                ? null
                : payload.Commands[i].DefaultParameterSetName;
            Assert.Equal(expected, result.Tools[i].RequiredParameterSet);
        }
    }

    [Fact]
    public void Map_RealPayload_SchemaIsObjectWithPropertiesAndRequiredList()
    {
        var payload = LoadFixture();

        var result = CommandMetadataMapper.Map(payload);

        Assert.All(result.Tools, t =>
        {
            Assert.Equal("object", t.Schema.Type);
            var mandatoryNames = t.Parameters.Where(p => p.IsMandatory).Select(p => p.Name).ToArray();
            Assert.Equal(mandatoryNames, t.Schema.Required.ToArray());
        });
    }

    [Fact]
    public void Map_SyntheticPayload_MutuallyExclusiveParametersHaveDistinctParameterSets()
    {
        var payload = new BinaryIntrospectionPayload
        {
            ModuleName = "Sets",
            ModulePath = "n/a",
            Commands = new List<BinaryCommandPayload>
            {
                new()
                {
                    Name = "Add-Thing",
                    DefaultParameterSetName = "Path",
                    Parameters = new List<BinaryParameterPayload>
                    {
                        new() { Name = "Path", Type = "System.String[]", ParameterSets = new List<string> { "Path" } },
                        new() { Name = "LiteralPath", Type = "System.String[]", ParameterSets = new List<string> { "LiteralPath" } },
                        new() { Name = "Value", Type = "System.String", ParameterSets = new List<string> { "Path", "LiteralPath" } },
                    },
                },
            },
        };

        var result = CommandMetadataMapper.Map(payload);
        var addThing = Assert.Single(result.Tools);
        var pathParam = addThing.Parameters.Single(p => p.Name == "Path");
        var literalParam = addThing.Parameters.Single(p => p.Name == "LiteralPath");

        Assert.Contains("Path", pathParam.ParameterSets);
        Assert.DoesNotContain("LiteralPath", pathParam.ParameterSets);
        Assert.Contains("LiteralPath", literalParam.ParameterSets);
        Assert.DoesNotContain("Path", literalParam.ParameterSets);

        var valueParam = addThing.Parameters.Single(p => p.Name == "Value");
        Assert.Contains("Path", valueParam.ParameterSets);
        Assert.Contains("LiteralPath", valueParam.ParameterSets);
    }

    [Fact]
    public void Map_NullPayload_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CommandMetadataMapper.Map(null!));
    }

    [Fact]
    public void Map_SyntheticPayload_EmptyCommandsProducesEmptyTools()
    {
        var payload = new BinaryIntrospectionPayload
        {
            ModuleName = "Empty",
            ModulePath = "n/a",
            Commands = new List<BinaryCommandPayload>(),
        };

        var result = CommandMetadataMapper.Map(payload);

        Assert.Equal("Empty", result.Module.Name);
        Assert.Empty(result.Tools);
    }

    [Fact]
    public void Map_SyntheticPayload_SecureStringParameterIsSecure()
    {
        var payload = new BinaryIntrospectionPayload
        {
            ModuleName = "Sec",
            ModulePath = "n/a",
            Commands = new List<BinaryCommandPayload>
            {
                new()
                {
                    Name = "Get-Cred",
                    Parameters = new List<BinaryParameterPayload>
                    {
                        new() { Name = "Credential", Type = "System.Management.Automation.PSCredential" },
                    },
                },
            },
        };

        var result = CommandMetadataMapper.Map(payload);

        var cred = result.Tools[0].Parameters.Single(p => p.Name == "Credential");
        Assert.True(cred.IsSecure);
        Assert.Equal("PSCredential", cred.Type);
    }

    [Fact]
    public void Map_SyntheticPayload_NullParameterTypeFallsBackToObject()
    {
        var payload = new BinaryIntrospectionPayload
        {
            ModuleName = "NullType",
            ModulePath = "n/a",
            Commands = new List<BinaryCommandPayload>
            {
                new()
                {
                    Name = "Get-Foo",
                    Parameters = new List<BinaryParameterPayload>
                    {
                        new() { Name = "Value", Type = null! },
                    },
                },
            },
        };

        var result = CommandMetadataMapper.Map(payload);
        var parameter = Assert.Single(result.Tools[0].Parameters);
        var property = Assert.Single(result.Tools[0].Schema.Properties);

        Assert.Equal("object", parameter.Type);
        Assert.False(parameter.IsSecure);
        Assert.Equal("object", property.Type);
        Assert.Null(property.Schema);
    }

    [Fact]
    public void Map_SyntheticPayload_SchemaTypesUseJsonSchemaMappings()
    {
        var payload = new BinaryIntrospectionPayload
        {
            ModuleName = "Shapes",
            ModulePath = "n/a",
            Commands = new List<BinaryCommandPayload>
            {
                new()
                {
                    Name = "Get-Shapes",
                    Parameters = new List<BinaryParameterPayload>
                    {
                        new() { Name = "Name", Type = "System.String" },
                        new() { Name = "Count", Type = "System.Int32" },
                        new() { Name = "Ratio", Type = "System.Double" },
                        new() { Name = "Enabled", Type = "System.Boolean" },
                        new() { Name = "Force", Type = "System.Management.Automation.SwitchParameter" },
                        new() { Name = "Tags", Type = "System.String[]" },
                    },
                },
            },
        };

        var result = CommandMetadataMapper.Map(payload);
        var schema = result.Tools[0].Schema;

        Assert.Equal("string", schema.Properties.Single(p => p.Name == "Name").Type);
        Assert.Equal("integer", schema.Properties.Single(p => p.Name == "Count").Type);
        Assert.Equal("number", schema.Properties.Single(p => p.Name == "Ratio").Type);
        Assert.Equal("boolean", schema.Properties.Single(p => p.Name == "Enabled").Type);
        Assert.Equal("boolean", schema.Properties.Single(p => p.Name == "Force").Type);

        var tags = schema.Properties.Single(p => p.Name == "Tags");
        Assert.Equal("array", tags.Type);
        Assert.NotNull(tags.Schema);
        Assert.NotNull(tags.Schema!.Items);
        Assert.Equal("string", tags.Schema.Items!.Type);
    }

    [Fact]
    public void Map_SyntheticPayload_OutputTypeFirstStringMapped()
    {
        var payload = new BinaryIntrospectionPayload
        {
            ModuleName = "Out",
            ModulePath = "n/a",
            Commands = new List<BinaryCommandPayload>
            {
                new()
                {
                    Name = "Get-Foo",
                    OutputType = new List<string> { "MyApp.Foo", "System.String" },
                    Parameters = new List<BinaryParameterPayload>(),
                },
            },
        };

        var result = CommandMetadataMapper.Map(payload);

        Assert.NotNull(result.Tools[0].Output);
        Assert.Equal("MyApp.Foo", result.Tools[0].Output!.OutputTypeName);
        Assert.Null(result.Tools[0].Output!.OutputTypeArguments);
    }

    [Fact]
    public void Map_SyntheticPayload_EmptyOutputTypeProducesNullOutput()
    {
        var payload = new BinaryIntrospectionPayload
        {
            ModuleName = "Out",
            ModulePath = "n/a",
            Commands = new List<BinaryCommandPayload>
            {
                new()
                {
                    Name = "Get-Foo",
                    OutputType = new List<string>(),
                    Parameters = new List<BinaryParameterPayload>(),
                },
            },
        };

        var result = CommandMetadataMapper.Map(payload);

        Assert.Null(result.Tools[0].Output);
    }

    private static BinaryIntrospectionPayload LoadFixture() => RealFixture.Value;

    private static BinaryIntrospectionPayload LoadFixtureCore()
    {
        var bytes = FixtureResourceLoader.LoadBytes(
            FixtureResourceLoader.BinaryMetadataMicrosoftPowerShellManagement);
        return BinaryIntrospectionPayloadSerializer.Deserialize(bytes);
    }
}

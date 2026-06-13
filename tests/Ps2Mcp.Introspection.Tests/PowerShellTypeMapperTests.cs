using Ps2Mcp.Core;

namespace Ps2Mcp.Introspection.Tests;

public sealed class PowerShellTypeMapperTests
{
    [Theory]
    [InlineData("string", "string")]
    [InlineData("System.String", "string")]
    [InlineData("byte", "integer")]
    [InlineData("System.Byte", "integer")]
    [InlineData("sbyte", "integer")]
    [InlineData("System.SByte", "integer")]
    [InlineData("short", "integer")]
    [InlineData("System.Int16", "integer")]
    [InlineData("ushort", "integer")]
    [InlineData("System.UInt16", "integer")]
    [InlineData("int", "integer")]
    [InlineData("System.Int32", "integer")]
    [InlineData("uint", "integer")]
    [InlineData("System.UInt32", "integer")]
    [InlineData("long", "integer")]
    [InlineData("System.Int64", "integer")]
    [InlineData("ulong", "integer")]
    [InlineData("System.UInt64", "integer")]
    [InlineData("double", "number")]
    [InlineData("System.Double", "number")]
    [InlineData("decimal", "number")]
    [InlineData("System.Decimal", "number")]
    [InlineData("float", "number")]
    [InlineData("System.Single", "number")]
    [InlineData("bool", "boolean")]
    [InlineData("System.Boolean", "boolean")]
    [InlineData("switch", "boolean")]
    [InlineData("System.Management.Automation.SwitchParameter", "boolean")]
    public void Map_PrimitiveTypes_ReturnExpectedSchemaTypes(string powerShellType, string expectedSchemaType)
    {
        var result = PowerShellTypeMapper.Map(powerShellType);

        Assert.Equal(expectedSchemaType, result.Type);
        Assert.Null(result.Schema);
    }

    [Fact]
    public void Map_ArrayType_ReturnsArraySchemaWithMappedItemType()
    {
        var result = PowerShellTypeMapper.Map("System.String[]");

        Assert.Equal("array", result.Type);
        Assert.NotNull(result.Schema);
        Assert.Equal("array", result.Schema!.Type);
        Assert.NotNull(result.Schema.Items);
        Assert.Equal("string", result.Schema.Items!.Type);
        Assert.Empty(result.Schema.Properties);
        Assert.Empty(result.Schema.Required);
    }

    [Fact]
    public void Map_UnknownType_PreservesHumanizedTypeUntilFallbackPhase()
    {
        var result = PowerShellTypeMapper.Map("System.ServiceProcess.ServiceController");

        Assert.Equal("ServiceController", result.Type);
        Assert.Null(result.Schema);
    }

    [Fact]
    public void Map_ArrayOfUnknownType_PreservesHumanizedItemTypeUntilFallbackPhase()
    {
        var result = PowerShellTypeMapper.Map("System.ServiceProcess.ServiceController[]");

        Assert.Equal("array", result.Type);
        Assert.NotNull(result.Schema);
        Assert.NotNull(result.Schema!.Items);
        Assert.Equal("ServiceController", result.Schema.Items!.Type);
    }

    [Theory]
    [InlineData("SecureString", true)]
    [InlineData("securestring", true)]
    [InlineData("System.Security.SecureString", true)]
    [InlineData("PSCredential", true)]
    [InlineData("pscredential", true)]
    [InlineData("System.Management.Automation.PSCredential", true)]
    [InlineData("string", false)]
    [InlineData("System.String", false)]
    [InlineData("NetworkCredential", false)]
    public void IsSecureType_ReturnsExpectedResult(string powerShellType, bool expected)
    {
        var result = PowerShellTypeMapper.IsSecureType(powerShellType);

        Assert.Equal(expected, result);
    }
}

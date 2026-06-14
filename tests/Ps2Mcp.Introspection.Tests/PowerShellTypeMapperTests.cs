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
        Assert.Null(result.ComplexType);
        Assert.Null(result.Schema);
    }

    [Fact]
    public void Map_ArrayType_ReturnsArraySchemaWithMappedItemType()
    {
        var result = PowerShellTypeMapper.Map("System.String[]");

        Assert.Equal("array", result.Type);
        Assert.Null(result.ComplexType);
        Assert.NotNull(result.Schema);
        Assert.Equal("array", result.Schema!.Type);
        Assert.NotNull(result.Schema.Items);
        Assert.Equal("string", result.Schema.Items!.Type);
        Assert.Empty(result.Schema.Properties);
        Assert.Empty(result.Schema.Required);
    }

    [Fact]
    public void Map_UnknownType_FallsBackToObjectWithComplexTypeMarker()
    {
        var result = PowerShellTypeMapper.Map("System.ServiceProcess.ServiceController");

        Assert.Equal("object", result.Type);
        Assert.Equal("ServiceController", result.ComplexType);
        Assert.Null(result.Schema);
    }

    [Fact]
    public void Map_ArrayOfUnknownType_FallsBackToObjectWithComplexTypeOnItemSchema()
    {
        var result = PowerShellTypeMapper.Map("System.ServiceProcess.ServiceController[]");

        Assert.Equal("array", result.Type);
        Assert.Null(result.ComplexType);
        Assert.NotNull(result.Schema);
        Assert.NotNull(result.Schema!.Items);
        Assert.Equal("object", result.Schema.Items!.Type);
        Assert.Equal("ServiceController", result.Schema.Items!.ComplexType);
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

    [Theory]
    [InlineData("string")]
    [InlineData("int")]
    [InlineData("bool")]
    [InlineData("double")]
    [InlineData("System.String")]
    [InlineData("System.Int32")]
    [InlineData("System.Boolean")]
    [InlineData("System.Double")]
    public void Map_KnownPrimitive_HasNoComplexType(string powerShellType)
    {
        var result = PowerShellTypeMapper.Map(powerShellType);

        Assert.Null(result.ComplexType);
    }

    [Fact]
    public void Map_ExplicitObjectType_HasNoComplexType()
    {
        var result = PowerShellTypeMapper.Map("object");

        Assert.Equal("object", result.Type);
        Assert.Null(result.ComplexType);
        Assert.Null(result.Schema);
    }

    [Theory]
    [InlineData("Object")]
    [InlineData("OBJECT")]
    [InlineData("System.Object")]
    public void Map_CapitalizedObject_HasNoComplexType(string powerShellType)
    {
        var result = PowerShellTypeMapper.Map(powerShellType);

        Assert.Equal("object", result.Type);
        Assert.Null(result.ComplexType);
    }

    [Fact]
    public void Map_NullableComplexType_FallsBackToObject()
    {
        var result = PowerShellTypeMapper.Map("System.Nullable[System.ServiceProcess.ServiceController]");

        Assert.Equal("object", result.Type);
        Assert.Equal("Nullable[ServiceController]", result.ComplexType);
    }

    [Fact]
    public void Map_NestedArrayOfComplexType_PreservesComplexTypeOnDeepestItem()
    {
        var result = PowerShellTypeMapper.Map("System.ServiceProcess.ServiceController[][]");

        Assert.Equal("array", result.Type);
        Assert.NotNull(result.Schema);
        Assert.Equal("array", result.Schema!.Type);
        Assert.NotNull(result.Schema.Items);
        Assert.Equal("array", result.Schema.Items!.Type);
        Assert.NotNull(result.Schema.Items.Items);
        Assert.Equal("object", result.Schema.Items.Items!.Type);
        Assert.Equal("ServiceController", result.Schema.Items.Items!.ComplexType);
    }

    [Fact]
    public void Map_ScalarComplexType_HasNullSchema()
    {
        var result = PowerShellTypeMapper.Map("MyApp.CustomType");

        Assert.Equal("object", result.Type);
        Assert.Equal("MyApp.CustomType", result.ComplexType);
        Assert.Null(result.Schema);
    }

    [Fact]
    public void Map_ToSchemaDefinition_SetsComplexTypeOnSchema()
    {
        var mapping = PowerShellTypeMapper.Map("System.ServiceProcess.ServiceController");
        var schema = mapping.ToSchemaDefinition();

        Assert.Equal("object", schema.Type);
        Assert.Equal("ServiceController", schema.ComplexType);
    }

    [Fact]
    public void Map_PrimitiveToSchemaDefinition_HasNullComplexType()
    {
        var mapping = PowerShellTypeMapper.Map("int");
        var schema = mapping.ToSchemaDefinition();

        Assert.Equal("integer", schema.Type);
        Assert.Null(schema.ComplexType);
    }
}

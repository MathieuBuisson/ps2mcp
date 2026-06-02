using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class SchemaPropertyTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var enumValues = ImmutableArray.Create("Red", "Green", "Blue");

        var property = new SchemaProperty(
            Name: "Color",
            Type: "string",
            Enum: enumValues,
            Minimum: null,
            Maximum: null,
            Pattern: null,
            Schema: null);

        Assert.Equal("Color", property.Name);
        Assert.Equal("string", property.Type);
        Assert.Equal(enumValues, property.Enum);
        Assert.Null(property.Minimum);
        Assert.Null(property.Maximum);
        Assert.Null(property.Pattern);
    }

    [Fact]
    public void Bounds_AreStringTypedForIntegerAndNumberCompatibility()
    {
        var property = new SchemaProperty("Count", "integer", null, "0", "100", null, null);

        Assert.Equal("0", property.Minimum);
        Assert.Equal("100", property.Maximum);
    }

    [Fact]
    public void ValueEquality_HoldsForIdenticalData()
    {
        var a = new SchemaProperty("Name", "string", null, null, null, null, null);
        var b = new SchemaProperty("Name", "string", null, null, null, null, null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueEquality_HoldsForDistinctArrayInstancesWithSameElements()
    {
        // Two distinct ImmutableArray<string> with the same enum values must compare equal via structural equality.
        var a = new SchemaProperty("Color", "string", ImmutableArray.Create("Red", "Green", "Blue"), null, null, null, null);
        var b = new SchemaProperty("Color", "string", ImmutableArray.Create("Red", "Green", "Blue"), null, null, null, null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueInequality_WhenPatternDiffers()
    {
        var a = new SchemaProperty("Name", "string", null, null, null, "^[a-z]+$", null);
        var b = new SchemaProperty("Name", "string", null, null, null, "^[0-9]+$", null);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void SubSchema_DefaultsToNullForScalarProperty()
    {
        var property = new SchemaProperty("Name", "string", null, null, null, null, null);

        Assert.Null(property.Schema);
    }

    [Fact]
    public void SubSchema_CarriesNestedObjectShape()
    {
        // A property of type=object where the value is a structured shape (the "complex object fallback").
        var inner = new SchemaProperty("Line1", "string", null, null, null, null, null);
        var innerSchema = new SchemaDefinition(
            "object",
            ImmutableArray.Create(inner),
            ImmutableArray.Create("Line1"),
            null);
        var property = new SchemaProperty("Address", "object", null, null, null, null, innerSchema);

        Assert.NotNull(property.Schema);
        Assert.Equal("object", property.Schema!.Type);
        Assert.Single(property.Schema.Properties);
        Assert.Equal("Line1", property.Schema.Properties[0].Name);
    }

    [Fact]
    public void SubSchema_ValueEqualityIsStructural()
    {
        // Two distinct sub-schemas with identical content must compare equal at the property level.
        var innerA = new SchemaProperty("Line1", "string", null, null, null, null, null);
        var innerSchemaA = new SchemaDefinition(
            "object",
            ImmutableArray.Create(innerA),
            ImmutableArray.Create("Line1"),
            null);
        var innerB = new SchemaProperty("Line1", "string", null, null, null, null, null);
        var innerSchemaB = new SchemaDefinition(
            "object",
            ImmutableArray.Create(innerB),
            ImmutableArray.Create("Line1"),
            null);
        var a = new SchemaProperty("Address", "object", null, null, null, null, innerSchemaA);
        var b = new SchemaProperty("Address", "object", null, null, null, null, innerSchemaB);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}

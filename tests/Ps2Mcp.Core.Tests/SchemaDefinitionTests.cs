using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Core.Tests;

public sealed class SchemaDefinitionTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var properties = ImmutableArray.Create(new SchemaProperty("Name", "string", null, null, null, null, null));
        var required = ImmutableArray.Create("Name");

        var schema = new SchemaDefinition("object", properties, required, null);

        Assert.Equal("object", schema.Type);
        Assert.Equal(properties, schema.Properties);
        Assert.Equal(required, schema.Required);
        Assert.Null(schema.Items);
    }

    [Fact]
    public void ValueEquality_HoldsForIdenticalData()
    {
        var a = new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null);
        var b = new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueEquality_HoldsForDistinctArrayInstancesWithSameElements()
    {
        // Both Properties and Required are distinct ImmutableArray instances with element-identical content.
        var propA = new SchemaProperty("Name", "string", null, null, null, null, null);
        var propB = new SchemaProperty("Name", "string", null, null, null, null, null);
        var a = new SchemaDefinition("object", ImmutableArray.Create(propA), ImmutableArray.Create("Name"), null);
        var b = new SchemaDefinition("object", ImmutableArray.Create(propB), ImmutableArray.Create("Name"), null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueInequality_WhenTypeDiffers()
    {
        var a = new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null);
        var b = new SchemaDefinition("array", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ArrayValuedParameter_ItemsDescribesElementShape()
    {
        // A parameter declared as [string[]]$Tags: the top-level schema is type=array and Items describes the element type.
        var stringElement = new SchemaDefinition("string", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null);
        var schema = new SchemaDefinition("array", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, stringElement);

        Assert.Equal("array", schema.Type);
        Assert.Same(stringElement, schema.Items);
    }

    [Fact]
    public void ArrayValuedParameter_ItemsEqualityIsStructural()
    {
        // Two distinct-but-identical element sub-schemas must yield equal parent schemas; otherwise verify-mode comparison breaks.
        var elementA = new SchemaDefinition("string", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null);
        var elementB = new SchemaDefinition("string", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null);
        var a = new SchemaDefinition("array", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, elementA);
        var b = new SchemaDefinition("array", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, elementB);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void NestedObjectFallback_IsRepresentable()
    {
        // A parameter declared as [Address]$Address where Address is a complex type: the top-level schema is type=object
        // and the property's Schema field describes the nested object's shape.
        var nestedStreet = new SchemaProperty("Street", "string", null, null, null, null, null);
        var nestedCity = new SchemaProperty("City", "string", null, null, null, null, null);
        var nestedSchema = new SchemaDefinition(
            "object",
            ImmutableArray.Create(nestedStreet, nestedCity),
            ImmutableArray.Create("Street", "City"),
            null);
        var addressProperty = new SchemaProperty("Address", "object", null, null, null, null, nestedSchema);
        var top = new SchemaDefinition(
            "object",
            ImmutableArray.Create(addressProperty),
            ImmutableArray<string>.Empty,
            null);

        Assert.Equal("object", top.Type);
        Assert.Single(top.Properties);
        Assert.Equal("Address", top.Properties[0].Name);
        Assert.Equal("object", top.Properties[0].Type);
        Assert.NotNull(top.Properties[0].Schema);
        Assert.Equal(2, top.Properties[0].Schema!.Properties.Length);
    }

    [Fact]
    public void NestedObjectFallback_RecursionIsSupported()
    {
        // A 2-level nested object: object > object > object. Confirms the IR is truly recursive.
        var deepestProperty = new SchemaProperty("Value", "string", null, null, null, null, null);
        var deepest = new SchemaDefinition(
            "object",
            ImmutableArray.Create(deepestProperty),
            ImmutableArray.Create("Value"),
            null);
        var middleProperty = new SchemaProperty("Inner", "object", null, null, null, null, deepest);
        var middle = new SchemaDefinition(
            "object",
            ImmutableArray.Create(middleProperty),
            ImmutableArray.Create("Inner"),
            null);
        var topProperty = new SchemaProperty("Middle", "object", null, null, null, null, middle);
        var top = new SchemaDefinition(
            "object",
            ImmutableArray.Create(topProperty),
            ImmutableArray.Create("Middle"),
            null);

        var middleSchema = top.Properties[0].Schema!;
        var deepestSchema = middleSchema.Properties[0].Schema!;
        var innerProperty = deepestSchema.Properties[0];
        Assert.Equal("Value", innerProperty.Name);
    }

    [Fact]
    public void HashCode_DiffersWhenOnlyPropertiesSequenceContentsDiffer()
    {
        // Regression: sequence contents must contribute to GetHashCode.
        var prop = new SchemaProperty("Name", "string", null, null, null, null, null);
        var a = new SchemaDefinition("object", ImmutableArray<SchemaProperty>.Empty, ImmutableArray<string>.Empty, null);
        var b = new SchemaDefinition("object", ImmutableArray.Create(prop), ImmutableArray<string>.Empty, null);

        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }
}

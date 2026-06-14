using System;
using System.Collections.Immutable;
using Ps2Mcp.Core;

namespace Ps2Mcp.Introspection.Tests;

public sealed class ValidationMapperTests
{
    [Fact]
    public void Map_NullAttributes_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ValidationMapper.Map(null!));
    }

    [Fact]
    public void Map_NoConstraints_ReturnsEmptyMapping()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "string",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: null,
            ValidateRangeMax: null,
            ValidatePattern: null,
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Null(result.Enum);
        Assert.Null(result.Minimum);
        Assert.Null(result.Maximum);
        Assert.Null(result.Pattern);
        Assert.False(result.HasConstraints);
    }

    [Fact]
    public void Map_ValidateSet_ProducesEnumArray()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "string",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: ImmutableArray.Create("prod", "staging", "dev"),
            ValidateRangeMin: null,
            ValidateRangeMax: null,
            ValidatePattern: null,
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.NotNull(result.Enum);
        Assert.Equal(new[] { "prod", "staging", "dev" }, result.Enum!.Value.ToArray());
        Assert.True(result.HasConstraints);
    }

    [Fact]
    public void Map_EmptyValidateSet_ProducesEmptyEnumArray()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "string",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: ImmutableArray<string>.Empty,
            ValidateRangeMin: null,
            ValidateRangeMax: null,
            ValidatePattern: null,
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.NotNull(result.Enum);
        Assert.Empty(result.Enum!.Value);
        Assert.True(result.HasConstraints);
    }

    [Fact]
    public void Map_ValidateSetNull_ReturnsNullEnum()
    {
        // ValidateSetValues is null when no [ValidateSet()] attribute was present
        var attrs = new ParameterAttributeInfo(
            Type: "string",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: null,
            ValidateRangeMax: null,
            ValidatePattern: null,
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Null(result.Enum);
    }

    [Fact]
    public void Map_ValidateRange_ProducesBounds()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "int",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: "1",
            ValidateRangeMax: "100",
            ValidatePattern: null,
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Equal("1", result.Minimum);
        Assert.Equal("100", result.Maximum);
        Assert.True(result.HasConstraints);
    }

    [Fact]
    public void Map_InvertedValidateRange_PreservesBoundsAsStrings()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "int",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: "100",
            ValidateRangeMax: "1",
            ValidatePattern: null,
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Equal("100", result.Minimum);
        Assert.Equal("1", result.Maximum);
    }

    [Fact]
    public void Map_ValidateRangeMinOnly_ProducesMinimumWithoutMaximum()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "int",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: "0",
            ValidateRangeMax: null,
            ValidatePattern: null,
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Equal("0", result.Minimum);
        Assert.Null(result.Maximum);
        Assert.True(result.HasConstraints);
    }

    [Fact]
    public void Map_ValidateRangeMaxOnly_ProducesMaximumWithoutMinimum()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "int",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: null,
            ValidateRangeMax: "1000",
            ValidatePattern: null,
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Null(result.Minimum);
        Assert.Equal("1000", result.Maximum);
        Assert.True(result.HasConstraints);
    }

    [Fact]
    public void Map_FloatingPointRange_PreservesInvariantCultureForm()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "double",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: "0.5",
            ValidateRangeMax: "1.5",
            ValidatePattern: null,
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Equal("0.5", result.Minimum);
        Assert.Equal("1.5", result.Maximum);
    }

    [Fact]
    public void Map_LargeIntegerRange_PreservesStringValue()
    {
        // Values beyond Int32.MaxValue survive extraction as invariant-culture strings
        var attrs = new ParameterAttributeInfo(
            Type: "long",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: "0",
            ValidateRangeMax: "2147483648",
            ValidatePattern: null,
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Equal("0", result.Minimum);
        Assert.Equal("2147483648", result.Maximum);
    }

    [Fact]
    public void Map_ValidatePattern_ProducesPatternString()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "string",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: null,
            ValidateRangeMax: null,
            ValidatePattern: "^[A-Z][a-zA-Z0-9]*$",
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Equal("^[A-Z][a-zA-Z0-9]*$", result.Pattern);
        Assert.True(result.HasConstraints);
    }

    [Fact]
    public void Map_InvalidRegexPattern_PreservesPatternString()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "string",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: null,
            ValidateRangeMax: null,
            ValidatePattern: "(unclosed",
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Equal("(unclosed", result.Pattern);
    }

    [Fact]
    public void Map_ComplexRegexPattern_PreservesVerbatim()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "string",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: null,
            ValidateRangeMax: null,
            ValidatePattern: @"^(?<user>[a-zA-Z0-9._%+-]+)@(?<domain>[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})$",
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Equal(@"^(?<user>[a-zA-Z0-9._%+-]+)@(?<domain>[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})$", result.Pattern);
    }

    [Fact]
    public void Map_MultipleValidatorsOnSameAttribute_AllConstraintsPreserved()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "string",
            IsMandatory: true,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: ImmutableArray.Create("a", "b", "c"),
            ValidateRangeMin: "1",
            ValidateRangeMax: "10",
            ValidatePattern: "^[a-c]$",
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.Equal(new[] { "a", "b", "c" }, result.Enum!.Value.ToArray());
        Assert.Equal("1", result.Minimum);
        Assert.Equal("10", result.Maximum);
        Assert.Equal("^[a-c]$", result.Pattern);
        Assert.True(result.HasConstraints);
    }

    [Fact]
    public void Map_AllowNull_DoesNotAffectValidationMapping()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "string",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: null,
            ValidateRangeMin: null,
            ValidateRangeMax: null,
            ValidatePattern: null,
            AllowNull: true,
            AllowEmptyString: true,
            AllowEmptyCollection: true);

        var result = ValidationMapper.Map(attrs);

        Assert.Null(result.Enum);
        Assert.Null(result.Minimum);
        Assert.Null(result.Maximum);
        Assert.Null(result.Pattern);
        Assert.False(result.HasConstraints);
    }

    [Fact]
    public void Map_SingleValidateSetValue_ProducesSingleElementArray()
    {
        var attrs = new ParameterAttributeInfo(
            Type: "string",
            IsMandatory: false,
            ParameterSets: ImmutableArray<string>.Empty,
            Aliases: ImmutableArray<string>.Empty,
            ValidateSetValues: ImmutableArray.Create("only-one"),
            ValidateRangeMin: null,
            ValidateRangeMax: null,
            ValidatePattern: null,
            AllowNull: false,
            AllowEmptyString: false,
            AllowEmptyCollection: false);

        var result = ValidationMapper.Map(attrs);

        Assert.NotNull(result.Enum);
        var values = result.Enum!.Value.ToArray();
        Assert.Single(values);
        Assert.Equal("only-one", values[0]);
    }
}

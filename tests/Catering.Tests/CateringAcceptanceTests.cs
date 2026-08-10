using Catering.Contracts;
using Xunit;

namespace Catering.Tests;

public sealed class CateringAcceptanceTests
{
    [Fact]
    public void QuantityConversionUsesExactDecimalArithmetic()
    {
        var kilograms = new Quantity(1500.25m, MeasurementUnit.Gram).ConvertTo(MeasurementUnit.Kilogram);
        var litres = new Quantity(2500.5m, MeasurementUnit.Milliliter).ConvertTo(MeasurementUnit.Liter);

        Assert.Equal(1.50025m, kilograms.Value);
        Assert.Equal(2.5005m, litres.Value);
    }

    [Fact]
    public void QuantityConversionRejectsIncompatibleDimensions()
    {
        var exception = Assert.Throws<CateringRuleException>(
            () => new Quantity(1m, MeasurementUnit.Kilogram).ConvertTo(MeasurementUnit.Liter));

        Assert.Equal("incompatible_unit", exception.ErrorCode);
        Assert.Equal("Diese Einheiten können nicht ineinander umgerechnet werden.", exception.Message);
    }

    [Fact]
    public void QuantityExposesEverySupportedDimensionAndCompatibleUnit()
    {
        var grams = new Quantity(1m, MeasurementUnit.Gram);
        var kilograms = new Quantity(1m, MeasurementUnit.Kilogram);
        var milliliters = new Quantity(1m, MeasurementUnit.Milliliter);
        var liters = new Quantity(1m, MeasurementUnit.Liter);
        var pieces = new Quantity(1m, MeasurementUnit.Piece);
        var named = new Quantity(1m, MeasurementUnit.NamedCount, "  Bund\t Petersilie  ");

        Assert.Equal(QuantityDimension.Mass, grams.Dimension);
        Assert.Equal(QuantityDimension.Mass, kilograms.Dimension);
        Assert.Equal(QuantityDimension.Volume, milliliters.Dimension);
        Assert.Equal(QuantityDimension.Volume, liters.Dimension);
        Assert.Equal(QuantityDimension.Count, pieces.Dimension);
        Assert.Equal(QuantityDimension.Count, named.Dimension);
        Assert.Equal([MeasurementUnit.Gram, MeasurementUnit.Kilogram], grams.CompatibleUnits);
        Assert.Equal([MeasurementUnit.Milliliter, MeasurementUnit.Liter], liters.CompatibleUnits);
        Assert.Equal([MeasurementUnit.Piece], pieces.CompatibleUnits);
        Assert.Equal([MeasurementUnit.NamedCount], named.CompatibleUnits);
        Assert.Equal("Bund Petersilie", named.CountUnitName);
    }

    [Fact]
    public void CountQuantitiesConvertOnlyToTheSameCountKind()
    {
        var pieces = new Quantity(3m, MeasurementUnit.Piece).ConvertTo(MeasurementUnit.Piece);
        var named = new Quantity(2m, MeasurementUnit.NamedCount, "Bund")
            .ConvertTo(MeasurementUnit.NamedCount);

        Assert.Equal(3m, pieces.Value);
        Assert.Equal("Bund", named.CountUnitName);
        Assert.Equal("incompatible_unit", Assert.Throws<CateringRuleException>(() =>
            pieces.ConvertTo(MeasurementUnit.NamedCount, "Bund")).ErrorCode);
        Assert.Equal("incompatible_unit", Assert.Throws<CateringRuleException>(() =>
            named.ConvertTo(MeasurementUnit.NamedCount, "Kiste")).ErrorCode);
    }

    [Theory]
    [InlineData("zero", 0, MeasurementUnit.Gram, null, "invalid_quantity")]
    [InlineData("negative", -1, MeasurementUnit.Gram, null, "invalid_quantity")]
    [InlineData("missing-name", 1, MeasurementUnit.NamedCount, null, "count_unit_required")]
    [InlineData("blank-name", 1, MeasurementUnit.NamedCount, "  ", "count_unit_required")]
    [InlineData("unexpected-name", 1, MeasurementUnit.Piece, "Bund", "count_unit_not_allowed")]
    public void QuantityRejectsInvalidConstruction(string _, int value, MeasurementUnit unit,
        string? name, string expectedCode)
        => Assert.Equal(expectedCode,
            Assert.Throws<CateringRuleException>(() => new Quantity(value, unit, name)).ErrorCode);
}

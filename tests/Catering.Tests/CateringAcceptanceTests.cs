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
}

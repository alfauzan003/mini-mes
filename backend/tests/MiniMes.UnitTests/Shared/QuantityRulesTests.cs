using MiniMes.Api.Shared.Quantities;
using MiniMes.Api.Shared.Results;

namespace MiniMes.UnitTests.Shared;

public class QuantityRulesTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("0.001")]
    [InlineData("1180.5")]
    [InlineData("999999999.999")]
    public void Quantity_within_scale_and_column_fits(string value) =>
        Assert.True(QuantityRules.Fits(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("0.0004")]
    [InlineData("1.0005")]
    [InlineData("1000000000")]
    [InlineData("999999999.9991")]
    public void Quantity_with_more_decimals_or_beyond_the_column_does_not_fit(string value)
    {
        var qty = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.False(QuantityRules.Fits(qty));
        Assert.Equal(ErrorCodes.InvalidQuantity, QuantityRules.CheckFits(qty, "Good quantity")!.Code);
    }

    [Fact]
    public void A_fitting_quantity_has_no_error() => Assert.Null(QuantityRules.CheckFits(10m, "Good quantity"));
}

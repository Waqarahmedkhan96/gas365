using Gas365.Domain;

namespace Gas365.UnitTests;

public sealed class SaleCalculationTests
{
    [Fact]
    public void RoundingAndDiscountsReconcileExactly()
    {
        var a = SaleCalculation.Calculate(3.125m, 123.456789m, 5.80m);
        Assert.Equal(385.80m, a.Gross); Assert.Equal(380m, a.Net);
        Assert.Equal(a.Gross, a.Net + a.Discount);
        Assert.Equal(1.01m, SaleCalculation.Calculate(1m, 1.005m, 0).Net);
        Assert.Equal(0m, SaleCalculation.Calculate(1m, 1.005m, 1.01m).Net);
    }
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 10, 11)]
    [InlineData(1, 10, -1)]
    [InlineData(1, 10, 0.001)]
    public void InvalidAmountsFail(decimal quantity, decimal price, decimal discount) =>
        Assert.Throws<ArgumentException>(() => SaleCalculation.Calculate(quantity, price, discount));
}

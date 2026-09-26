using Gas365.Domain;

namespace Gas365.UnitTests;

public sealed class CylinderSettlementTests
{
    [Theory]
    [InlineData(5000, 2000, 0, 3000, 0)]
    [InlineData(5000, 1275.50, 0, 3724.50, 0)]
    [InlineData(0, 2000, 0, 0, 2000)]
    [InlineData(5000, 4000, 1500, 0, 500)]
    [InlineData(5000, 0, 0, 5000, 0)]
    [InlineData(5000, 5000, 0, 0, 0)]
    [InlineData(0, 0, 0, 0, 0)]
    public void ReturnSeparatesRevenueRefundLiabilityAndReceivable(decimal deposit, decimal usage, decimal damage, decimal refund, decimal collect)
    {
        var result = CylinderSettlement.Calculate(deposit, usage, damage);
        Assert.Equal(usage + damage, result.Revenue);
        Assert.Equal(refund, result.RefundDue);
        Assert.Equal(collect, result.AmountToCollect);
        Assert.Equal(deposit, result.AppliedDeposit + result.RefundDue);
        Assert.Equal(result.Revenue, result.AppliedDeposit + result.AmountToCollect);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(1000000000.01)]
    public void InvalidMoneyIsRejectedInEveryInput(decimal invalid)
    {
        Assert.Throws<ArgumentException>(() => CylinderSettlement.Calculate(invalid, 0, 0));
        Assert.Throws<ArgumentException>(() => CylinderSettlement.Calculate(0, invalid, 0));
        Assert.Throws<ArgumentException>(() => CylinderSettlement.Calculate(0, 0, invalid));
    }

    [Fact]
    public void MaximumChargesConserveMoneyWithoutOverflow()
    {
        var result = CylinderSettlement.Calculate(1000000000m, 1000000000m, 1000000000m);
        Assert.Equal(2000000000m, result.Revenue);
        Assert.Equal(1000000000m, result.AmountToCollect);
        Assert.Equal(0, result.RefundDue);
    }
}

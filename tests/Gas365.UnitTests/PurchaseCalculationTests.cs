using Gas365.Domain;

namespace Gas365.UnitTests;

public sealed class PurchaseCalculationTests
{
    [Theory]
    [InlineData("Quantity", 20, 40)]
    [InlineData("Weight", 40, 20)]
    [InlineData("Value", 12, 48)]
    public void AllocationUsesSelectedBasisAndConservesValue(string rule, decimal first, decimal second)
    {
        var result = PurchaseCalculation.Allocate([new(10, 100, 20), new(20, 200, 10)], 60, rule);
        Assert.Equal(first, result[0].LandedCost); Assert.Equal(second, result[1].LandedCost);
        Assert.Equal(5060, result.Sum(x => x.Value)); Assert.Equal(60, result.Sum(x => x.LandedCost));
    }
    [Fact]
    public void ResidualCentsAreStableAndNeverNegative()
    {
        for (var count = 1; count <= 100; count++)
        {
            var result = PurchaseCalculation.Allocate(Enumerable.Repeat(new PurchaseCostInput(1, 0, 1), count).ToArray(), 0.51m, "Quantity");
            Assert.Equal(0.51m, result.Sum(x => x.LandedCost)); Assert.All(result, x => Assert.True(x.LandedCost >= 0));
        }
        var tiny = PurchaseCalculation.Allocate([new(3, 0.005m, 1)], 0.01m, "Value");
        Assert.Equal(0.02m, tiny[0].BaseValue); Assert.Equal(0.03m, tiny[0].Value); Assert.Equal(0.01m, tiny[0].UnitCost);
    }
    [Fact]
    public void RejectsInvalidAllocationAndSupportsFreeReceipts()
    {
        Assert.Throws<ArgumentException>(() => PurchaseCalculation.Allocate([new(1, 0, 0)], 1, "Value"));
        Assert.Throws<ArgumentException>(() => PurchaseCalculation.Allocate([new(1, 1, 0), new(1, 1, 1)], 1, "Weight"));
        Assert.Throws<ArgumentException>(() => PurchaseCalculation.Allocate([new(0, 1, 1)], 1, "Quantity"));
        Assert.Throws<ArgumentException>(() => PurchaseCalculation.Allocate([new(1, 1, 1)], 0.001m, "Quantity"));
        Assert.Equal(0, PurchaseCalculation.Allocate([new(1, 0, 0)], 0, "Value")[0].Value);
    }
}

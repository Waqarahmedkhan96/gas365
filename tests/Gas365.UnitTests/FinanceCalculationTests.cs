using Gas365.Domain;

namespace Gas365.UnitTests;

public sealed class FinanceCalculationTests
{
    [Fact]
    public void ClosingReconcilesAllSignedFlowsExactly()
    {
        // Opening + sales + collections + loans/owner funds + deposit - goods - expenses - supplier - repayment - bank deposit - refund.
        Assert.Equal(665.75m, FinanceCalculation.ExpectedClose(1000m, [100m, 50m, 200m, 25.75m, -300m, -10m, -100m, -150m, -125m, -25m]));
    }
    [Theory]
    [InlineData(-1, "Current")]
    [InlineData(0, "Current")]
    [InlineData(1, "1–30")]
    [InlineData(30, "1–30")]
    [InlineData(31, "31–60")]
    [InlineData(60, "31–60")]
    [InlineData(61, "61–90")]
    [InlineData(90, "61–90")]
    [InlineData(91, "90+")]
    public void AgingBoundaries(int days, string expected)
    {
        var due = new DateOnly(2026, 1, 1);
        Assert.Equal(expected, FinanceCalculation.AgingBucket(due, due.AddDays(days)));
    }
}

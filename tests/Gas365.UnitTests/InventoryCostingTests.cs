using Gas365.Domain;

namespace Gas365.UnitTests;

public sealed class InventoryCostingTests
{
    private readonly WeightedAverageCosting costing = new();
    [Fact]
    public void WeightedAverageSnapshotsAndResidualsReconcile()
    {
        var value = costing.ReceiptValue(10, 100) + costing.ReceiptValue(10, 120);
        Assert.Equal(110, costing.Average(20, value));
        var snapshot = costing.IssueValue(20, value, 5);
        Assert.Equal(550, snapshot); Assert.Equal(1650, value - snapshot);
        Assert.Equal(550, snapshot); // Subsequent receipt costs never rewrite this value.
        Assert.Equal(0.01m, costing.IssueValue(3, 0.02m, 1));
        Assert.Equal(0.01m, costing.IssueValue(2, 0.01m, 2));
        Assert.Equal(0, costing.Average(0, 0));
    }
    [Fact]
    public void RoundingIsExplicitAndInvalidIssuesAreRejected()
    {
        Assert.Equal(1.01m, costing.ReceiptValue(1, 1.005m));
        Assert.Throws<InvalidOperationException>(() => costing.IssueValue(1, 10, 2));
        Assert.Throws<InvalidOperationException>(() => costing.IssueValue(0, 0, 1));
        Assert.Throws<InvalidOperationException>(() => costing.IssueValue(1, 10, 0));
    }
}

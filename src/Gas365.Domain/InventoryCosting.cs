namespace Gas365.Domain;

public interface IInventoryCosting
{
    decimal ReceiptValue(decimal quantity, decimal unitCost);
    decimal IssueValue(decimal balanceQuantity, decimal balanceValue, decimal quantity);
    decimal Average(decimal quantity, decimal value);
}

public sealed class WeightedAverageCosting : IInventoryCosting
{
    public decimal ReceiptValue(decimal quantity, decimal unitCost) => decimal.Round(quantity * unitCost, 2, MidpointRounding.AwayFromZero);
    public decimal IssueValue(decimal balanceQuantity, decimal balanceValue, decimal quantity)
    {
        if (quantity <= 0 || balanceQuantity < quantity || balanceValue < 0) throw new InvalidOperationException("Insufficient stock or invalid issue.");
        // Exhaustion consumes the residual, so zero quantity always has zero value.
        return quantity == balanceQuantity ? balanceValue : decimal.Round(balanceValue * quantity / balanceQuantity, 2, MidpointRounding.AwayFromZero);
    }
    public decimal Average(decimal quantity, decimal value) => quantity == 0 ? 0 : decimal.Round(value / quantity, 6, MidpointRounding.AwayFromZero);
}

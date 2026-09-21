namespace Gas365.Domain;

public sealed class SaleDraft : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid PriceListId { get; set; }
    public string Reference { get; set; } = "";
    public string Content { get; set; } = "";
    public string Snapshot { get; set; } = "";
    public uint Version { get; set; }
}

public sealed record SaleAmounts(decimal Gross, decimal Discount, decimal Net);
public static class SaleCalculation
{
    public static SaleAmounts Calculate(decimal quantity, decimal unitPrice, decimal discount)
    {
        if (quantity <= 0 || quantity > 1000000m || unitPrice < 0 || unitPrice > 1000000m || decimal.Round(unitPrice, 6) != unitPrice)
            throw new ArgumentException("Invalid sale quantity or unit price.");
        var gross = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        if (discount < 0 || discount > gross || decimal.Round(discount, 2) != discount)
            throw new ArgumentException("Discount must be a two-place PKR amount no greater than the line gross.");
        return new(gross, discount, gross - discount);
    }
}

namespace Gas365.Domain;

public sealed class Supplier : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public bool Active { get; set; } = true;
    public uint Version { get; set; }
}
public sealed class PurchaseDraft : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SupplierId { get; set; }
    public string Content { get; set; } = "";
    public string Snapshot { get; set; } = "";
    public uint Version { get; set; }
}
// A posted snapshot is separate from its editable draft. Status is derived from entries.
public sealed class PurchaseDocument : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid DraftId { get; set; }
    public Guid StockDocumentId { get; set; }
    public Guid? CorrectsId { get; set; }
    public string SupplierName { get; set; } = "";
    public string Content { get; set; } = "";
    public decimal Total { get; set; }
}
// Payable credit is positive; a payment is negative. Cash outflow is negative.
// Each payment is allocated to exactly one purchase in Phase 05.
public sealed class PurchaseEntry : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid ActorId { get; set; }
    public Guid? ReversesId { get; set; }
    public string Kind { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Reason { get; set; } = "";
    public decimal Payable { get; set; }
    public decimal Cash { get; set; }
    public DateTimeOffset PostedAt { get; set; }
    public DateOnly BusinessDate { get; set; }
}

public sealed record PurchaseCostInput(decimal Quantity, decimal UnitCost, decimal WeightKg);
public sealed record PurchaseCostLine(decimal BaseValue, decimal LandedCost, decimal Value, decimal UnitCost);
public static class PurchaseCalculation
{
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    public static PurchaseCostLine[] Allocate(IReadOnlyList<PurchaseCostInput> lines, decimal charges, string rule)
    {
        if (lines.Count is < 1 or > 100 || charges < 0 || Money(charges) != charges || charges > 1000000000m)
            throw new ArgumentException("Provide 1–100 lines and valid two-place landed charges.");
        if (lines.Any(x => x.Quantity <= 0 || x.Quantity > 1000000m || x.UnitCost < 0 || x.UnitCost > 1000000m || x.WeightKg < 0 || x.WeightKg > 1000000000m))
            throw new ArgumentException("Invalid purchase quantity, unit cost or weight.");
        var bases = lines.Select(x => Money(x.Quantity * x.UnitCost)).ToArray();
        var weights = rule switch
        {
            "Quantity" => lines.Select(x => x.Quantity).ToArray(),
            "Weight" => lines.Select(x => x.WeightKg).ToArray(),
            "Value" => bases,
            _ => throw new ArgumentException("Choose Quantity, Weight or Value allocation.")
        };
        var total = weights.Sum();
        if (charges > 0 && (total == 0 || (rule == "Weight" && weights.Any(x => x <= 0))))
            throw new ArgumentException("Allocation needs a positive basis; weight allocation needs each line's total received weight.");
        // Allocate whole cents by largest remainder. Stable line order breaks ties.
        var exact = weights.Select(x => total == 0 ? 0 : charges * 100 * x / total).ToArray();
        var cents = exact.Select(decimal.Floor).ToArray();
        var remainder = (int)(charges * 100 - cents.Sum());
        foreach (var i in Enumerable.Range(0, lines.Count).OrderByDescending(i => exact[i] - cents[i]).ThenBy(i => i).Take(remainder)) cents[i]++;
        return lines.Select((x, i) => new PurchaseCostLine(bases[i], cents[i] / 100, bases[i] + cents[i] / 100,
            decimal.Round((bases[i] + cents[i] / 100) / x.Quantity, 6, MidpointRounding.AwayFromZero))).ToArray();
    }
}

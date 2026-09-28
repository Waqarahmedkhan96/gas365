namespace Gas365.Domain;

public sealed class MoneyAccount : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Cash";
    public bool DefaultCash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class ExpenseCategory : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public bool Capital { get; set; }
}
public sealed class FinanceEntry : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public long Sequence { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? ReversesId { get; set; }
    public Guid? SaleId { get; set; }
    public Guid? PurchaseId { get; set; }
    public Guid? SourceId { get; set; }
    public string Kind { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Cash { get; set; }
    public string Counterparty { get; set; } = "";
    public string Reason { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset PostedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateOnly BusinessDate { get; set; }
}
public sealed class RegisterEvent : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid AccountId { get; set; }
    public long Sequence { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string Kind { get; set; } = "";
    public decimal Opening { get; set; }
    public decimal Expected { get; set; }
    public decimal Counted { get; set; }
    public string Reason { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset PostedAt { get; set; } = DateTimeOffset.UtcNow;
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
}
public static class FinanceCalculation
{
    public static decimal ExpectedClose(decimal opening, IEnumerable<decimal> movements) => opening + movements.Sum();
    public static string AgingBucket(DateOnly due, DateOnly asOf) => (asOf.DayNumber - due.DayNumber) switch
    {
        <= 0 => "Current",
        <= 30 => "1–30",
        <= 60 => "31–60",
        <= 90 => "61–90",
        _ => "90+"
    };
}

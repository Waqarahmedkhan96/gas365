namespace Gas365.Domain;

public sealed class CylinderPolicy : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public Guid GasItemId { get; set; }
    public decimal GasQuantity { get; set; }
    public decimal SuggestedDeposit { get; set; }
    public string UsageTerms { get; set; } = "";
    public uint Version { get; set; }
}
public sealed class CustomerDepositArrangement : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public bool ZeroDepositApproved { get; set; }
    public Guid ApprovedBy { get; set; }
    public string Reason { get; set; } = "";
    public uint Version { get; set; }
}
public sealed class CylinderLoan : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SaleId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public string Serial { get; set; } = "";
    public decimal Deposit { get; set; }
    public string UsageTerms { get; set; } = "";
}
public sealed class CylinderLoanEntry : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid LoanId { get; set; }
    public long Sequence { get; set; }
    public Guid ActorId { get; set; }
    public string Kind { get; set; } = "";
    public decimal Cash { get; set; }
    public decimal Liability { get; set; }
    public decimal Receivable { get; set; }
    public decimal Revenue { get; set; }
    public decimal UsageFee { get; set; }
    public decimal DamageOrLossCharge { get; set; }
    public string Condition { get; set; } = "";
    public Guid? StockDocumentId { get; set; }
    public Guid? ApprovalId { get; set; }
    public Guid? ReversesId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset PostedAt { get; set; }
    public DateOnly BusinessDate { get; set; }
}

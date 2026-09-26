namespace Gas365.Domain;

public sealed class SaleDocument : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid DraftId { get; set; }
    public Guid StockDocumentId { get; set; }
    public string Reference { get; set; } = "";
    public string Content { get; set; } = "";
    public decimal Total { get; set; }
    public decimal Cogs { get; set; }
}

public sealed class SaleEntry : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SaleId { get; set; }
    public long Sequence { get; set; }
    public Guid ActorId { get; set; }
    public string Kind { get; set; } = "";
    public decimal Receivable { get; set; }
    public decimal Cash { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cogs { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset PostedAt { get; set; }
    public DateOnly BusinessDate { get; set; }
    public Guid? ReversesId { get; set; }
}

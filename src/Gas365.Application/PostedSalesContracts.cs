namespace Gas365.Application;

public sealed record SaleCylinderOptions(string[] IncomingSerials, decimal DepositPerCylinder);
public sealed record SalePostRequest(string IdempotencyKey, uint Version, decimal CashPaid, string[][] Serials, SaleCylinderOptions[]? Cylinders = null);
public sealed record SalePaymentRequest(string IdempotencyKey, decimal Amount, string Reason, Guid ExpectedLastEntryId);
public sealed record SaleReverseRequest(string IdempotencyKey, string Reason);
public sealed record SaleEntryDto(Guid Id, string Kind, decimal Receivable, decimal Cash, decimal Revenue, decimal? Cogs,
    string Reason, Guid ActorId, DateTimeOffset PostedAt, DateOnly BusinessDate, Guid? ReversesId);
public sealed record SaleDto(Guid Id, Guid DraftId, Guid BranchId, Guid CustomerId, string Reference, string Status,
    decimal Total, decimal Paid, decimal Outstanding, decimal? Cogs, decimal? GrossProfit, Guid StockDocumentId,
    SaleDraftSnapshot Snapshot, SaleEntryDto[] Entries, decimal DepositCollected = 0, Guid[]? LoanIds = null);
public interface ISalesAccess
{
    Task<PageDto<SaleDto>> SalesAsync(Guid branchId, Guid? customerId, int page, CancellationToken ct);
    Task<SaleDto> SaleAsync(Guid id, CancellationToken ct);
    Task<SaleDto> PostAsync(Guid id, SalePostRequest request, CancellationToken ct);
    Task<SaleDto> PayAsync(Guid id, SalePaymentRequest request, CancellationToken ct);
    Task<SaleDto> ReversePaymentAsync(Guid id, Guid entryId, SaleReverseRequest request, CancellationToken ct);
    Task<SaleDto> ReverseAsync(Guid id, SaleReverseRequest request, CancellationToken ct);
    Task<byte[]> ReceiptAsync(Guid id, CancellationToken ct);
}

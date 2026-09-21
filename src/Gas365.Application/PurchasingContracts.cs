namespace Gas365.Application;

public sealed record SupplierRequest(string Name, string Phone, string Address, bool Active = true, uint Version = 0);
public sealed record SupplierDto(Guid Id, string Name, string Phone, string Address, bool Active, uint Version);
public sealed record PurchaseLineRequest(Guid ItemId, Guid WarehouseId, string Bucket, decimal Quantity, decimal UnitCost, decimal WeightKg, string[] Serials);
public sealed record PurchaseDraftRequest(Guid BranchId, Guid SupplierId, string Reference, string Notes, string AllocationRule, decimal LandedCharges,
    PurchaseLineRequest[] Lines, Guid? CorrectsId = null, uint Version = 0);
public sealed record PurchaseLineDto(Guid ItemId, string Sku, string Name, string Unit, Guid WarehouseId, string Warehouse, string Bucket,
    decimal Quantity, decimal Price, decimal WeightKg, string[] Serials, decimal BaseValue, decimal LandedCost, decimal Value, decimal ReceiptUnitCost);
public sealed record PurchaseSnapshot(string Reference, string Notes, string AllocationRule, decimal LandedCharges, PurchaseLineDto[] Lines);
public sealed record PurchaseDto(Guid Id, Guid BranchId, Guid SupplierId, string SupplierName, string Status, uint Version, decimal Total, decimal Paid, decimal Outstanding,
    PurchaseSnapshot Snapshot, Guid? StockDocumentId, Guid? CorrectsId, PurchaseEntryDto[] Entries);
public sealed record PurchaseEntryDto(Guid Id, string Kind, decimal Payable, decimal Cash, string Reason, Guid ActorId, DateTimeOffset PostedAt, DateOnly BusinessDate, Guid? ReversesId);
public sealed record PurchasePostRequest(string IdempotencyKey, uint Version, decimal CashPaid);
public sealed record PurchasePaymentRequest(string IdempotencyKey, decimal Amount, string Reason, Guid ExpectedLastEntryId);
public sealed record PurchaseReverseRequest(string IdempotencyKey, string Reason);
public sealed record SupplierLedgerDto(Guid PurchaseId, PurchaseEntryDto Entry);
public sealed record SupplierProfileDto(SupplierDto Supplier, decimal Balance, PageDto<SupplierLedgerDto> Ledger);
public interface IPurchasingAccess
{
    Task<PageDto<SupplierDto>> SuppliersAsync(string? search, int page, CancellationToken ct);
    Task<SupplierDto> SaveSupplierAsync(Guid? id, SupplierRequest request, CancellationToken ct);
    Task<SupplierProfileDto> SupplierAsync(Guid id, Guid branchId, int page, CancellationToken ct);
    Task<PageDto<PurchaseDto>> PurchasesAsync(Guid branchId, int page, CancellationToken ct);
    Task<PurchaseDto> PurchaseAsync(Guid id, CancellationToken ct);
    Task<PurchaseDto> SaveDraftAsync(Guid? id, PurchaseDraftRequest request, CancellationToken ct);
    Task<PurchaseDto> PostAsync(Guid id, PurchasePostRequest request, CancellationToken ct);
    Task<PurchaseDto> PayAsync(Guid id, PurchasePaymentRequest request, CancellationToken ct);
    Task<PurchaseDto> ReversePaymentAsync(Guid id, Guid entryId, PurchaseReverseRequest request, CancellationToken ct);
    Task<PurchaseDto> ReverseAsync(Guid id, PurchaseReverseRequest request, CancellationToken ct);
    Task<byte[]> VoucherAsync(Guid id, CancellationToken ct);
}

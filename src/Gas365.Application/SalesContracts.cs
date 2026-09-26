namespace Gas365.Application;

public sealed record SaleLineRequest(Guid PriceId, uint PriceVersion, Guid WarehouseId, decimal Quantity, decimal Discount,
    string CylinderReference = "", decimal LossQuantity = 0, string LossReason = "", string SourceCylinderReference = "");
public sealed record SaleDraftRequest(Guid BranchId, Guid CustomerId, Guid PriceListId, string Reference, string Notes, SaleLineRequest[] Lines, uint Version = 0);
public sealed record SaleLineDto(Guid PriceId, uint PriceVersion, Guid ItemId, string Sku, string Name, string Mode, string Unit, Guid WarehouseId, string Warehouse, decimal Quantity, decimal UnitPrice, decimal Gross, decimal Discount, decimal Net,
    string CylinderReference = "", decimal LossQuantity = 0, string LossReason = "",
    uint CylinderPolicyVersion = 0, Guid? GasItemId = null, decimal GasQuantity = 0, decimal SuggestedDeposit = 0, string UsageTerms = "", string SourceCylinderReference = "");
public sealed record SaleDraftSnapshot(string CustomerName, string PriceListName, string Notes, SaleLineDto[] Lines, decimal Gross, decimal Discount, decimal Net);
public sealed record SaleDraftDto(Guid Id, Guid BranchId, Guid CustomerId, Guid PriceListId, string Reference, uint Version, SaleDraftSnapshot Snapshot);
public interface ISalesDraftAccess
{
    Task<PageDto<SaleDraftDto>> DraftsAsync(Guid branchId, int page, CancellationToken ct);
    Task<SaleDraftDto> DraftAsync(Guid id, CancellationToken ct);
    Task<SaleDraftDto> SaveAsync(Guid? id, SaleDraftRequest request, CancellationToken ct);
}

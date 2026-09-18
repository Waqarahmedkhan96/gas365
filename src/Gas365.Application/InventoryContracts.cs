namespace Gas365.Application;

public sealed record UnitRequest(string Code, int DecimalPlaces);
public sealed record UnitDto(Guid Id, string Code, int DecimalPlaces);
public sealed record CategoryRequest(string Name);
public sealed record CategoryDto(Guid Id, string Name);
public sealed record ItemRequest(string Sku, string Name, string Kind, Guid UnitId, Guid? CategoryId,
    string Brand, string Classification, decimal? CapacityKg, decimal? TareWeightKg,
    bool SerialTracked, decimal ReorderLevel, bool Active = true, uint Version = 0);
public sealed record ItemDto(Guid Id, string Sku, string Name, string Kind, Guid UnitId, Guid? CategoryId,
    string Brand, string Classification, decimal? CapacityKg, decimal? TareWeightKg,
    bool SerialTracked, decimal ReorderLevel, bool Active, uint Version);
public sealed record WarehouseRequest(Guid BranchId, string Name, string Location, bool Active = true, uint Version = 0);
public sealed record WarehouseDto(Guid Id, Guid BranchId, string Name, string Location, bool Active, uint Version);
public sealed record PageDto<T>(T[] Items, int Total, int Page, int PageSize);
public interface ICatalogAccess
{
    Task<UnitDto[]> UnitsAsync(CancellationToken ct);
    Task<UnitDto> CreateUnitAsync(UnitRequest request, CancellationToken ct);
    Task<CategoryDto[]> CategoriesAsync(CancellationToken ct);
    Task<CategoryDto> CreateCategoryAsync(CategoryRequest request, CancellationToken ct);
    Task<PageDto<ItemDto>> ItemsAsync(string? search, int page, int pageSize, CancellationToken ct);
    Task<ItemDto> ItemAsync(Guid id, CancellationToken ct);
    Task<ItemDto> SaveItemAsync(Guid? id, ItemRequest request, CancellationToken ct);
    Task<WarehouseDto[]> WarehousesAsync(CancellationToken ct);
    Task<WarehouseDto> SaveWarehouseAsync(Guid? id, WarehouseRequest request, CancellationToken ct);
}

public sealed record AssetRequest(Guid ItemId, string Serial, string Owner, string InspectionReference, DateOnly? InspectionDue, uint Version = 0);
public sealed record AssetDto(Guid Id, Guid ItemId, string Serial, string Owner, string InspectionReference, DateOnly? InspectionDue, uint Version);
// Decimal inputs are accepted as JSON numbers or strings. The API alone calculates valuations.
public sealed record PostStockRequest(string IdempotencyKey, string Kind, Guid ItemId, Guid WarehouseId,
    string Bucket, decimal Quantity, decimal? UnitCost, Guid? DestinationWarehouseId, string? DestinationBucket,
    string Reason, long ExpectedSequence, long DestinationExpectedSequence, string[] Serials);
public sealed record ReverseStockRequest(string IdempotencyKey, string Reason);
public sealed record StockBalanceDto(Guid ItemId, Guid WarehouseId, Guid BranchId, string Bucket, decimal Quantity, decimal WarehouseValue, decimal AverageCost, long Sequence);
public sealed record MovementDto(Guid Id, long Sequence, Guid DocumentId, Guid BranchId, Guid WarehouseId, Guid ItemId,
    string Bucket, decimal Quantity, decimal Value, decimal UnitCost, string[] Serials);
public sealed record DocumentDto(Guid Id, Guid BranchId, Guid ActorId, string Kind, string Reason, DateTimeOffset PostedAt, DateOnly BusinessDate,
    Guid? ReversesId, Guid? ReversedById, MovementDto[] Movements);
public interface IInventoryAccess
{
    Task<StockBalanceDto[]> BalancesAsync(Guid itemId, CancellationToken ct);
    Task<PageDto<MovementDto>> LedgerAsync(Guid itemId, Guid? warehouseId, int page, int pageSize, CancellationToken ct);
    Task<DocumentDto> DocumentAsync(Guid id, CancellationToken ct);
    Task<DocumentDto> PostAsync(PostStockRequest request, CancellationToken ct);
    Task<DocumentDto> ReverseAsync(Guid id, ReverseStockRequest request, CancellationToken ct);
    Task<AssetDto[]> AssetsAsync(Guid itemId, CancellationToken ct);
    Task<AssetDto> SaveAssetAsync(Guid? id, AssetRequest request, CancellationToken ct);
}

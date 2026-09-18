using Gas365.Application;
using Gas365.Domain;

namespace Gas365.Api;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspace/inventory").WithTags("Inventory").RequireAuthorization();
        group.MapGet("/units", (ICatalogAccess service, CancellationToken ct) => service.UnitsAsync(ct));
        group.MapPost("/units", (UnitRequest request, ICatalogAccess service, CancellationToken ct) => service.CreateUnitAsync(request, ct));
        group.MapGet("/categories", (ICatalogAccess service, CancellationToken ct) => service.CategoriesAsync(ct));
        group.MapPost("/categories", (CategoryRequest request, ICatalogAccess service, CancellationToken ct) => service.CreateCategoryAsync(request, ct));
        group.MapGet("/items", (ICatalogAccess service, CancellationToken ct, string? search = null, int page = 1, int pageSize = 20) => service.ItemsAsync(search, page, pageSize, ct));
        group.MapGet("/items/{id:guid}", (Guid id, ICatalogAccess service, CancellationToken ct) => service.ItemAsync(id, ct));
        group.MapPost("/items", (ItemRequest request, ICatalogAccess service, CancellationToken ct) => service.SaveItemAsync(null, request, ct));
        group.MapPut("/items/{id:guid}", (Guid id, ItemRequest request, ICatalogAccess service, CancellationToken ct) => service.SaveItemAsync(id, request, ct));
        group.MapGet("/warehouses", (ICatalogAccess service, CancellationToken ct) => service.WarehousesAsync(ct));
        group.MapPost("/warehouses", (WarehouseRequest request, ICatalogAccess service, CancellationToken ct) => service.SaveWarehouseAsync(null, request, ct));
        group.MapPut("/warehouses/{id:guid}", (Guid id, WarehouseRequest request, ICatalogAccess service, CancellationToken ct) => service.SaveWarehouseAsync(id, request, ct));
        group.MapGet("/items/{id:guid}/balances", (Guid id, IInventoryAccess service, CancellationToken ct) => service.BalancesAsync(id, ct));
        group.MapGet("/items/{id:guid}/ledger", (Guid id, IInventoryAccess service, CancellationToken ct, Guid? warehouseId = null, int page = 1, int pageSize = 20) => service.LedgerAsync(id, warehouseId, page, pageSize, ct));
        group.MapGet("/items/{id:guid}/assets", (Guid id, IInventoryAccess service, CancellationToken ct) => service.AssetsAsync(id, ct));
        group.MapPost("/assets", (AssetRequest request, IInventoryAccess service, CancellationToken ct) => service.SaveAssetAsync(null, request, ct));
        group.MapPut("/assets/{id:guid}", (Guid id, AssetRequest request, IInventoryAccess service, CancellationToken ct) => service.SaveAssetAsync(id, request, ct));
        group.MapGet("/documents/{id:guid}", (Guid id, IInventoryAccess service, CancellationToken ct) => service.DocumentAsync(id, ct));
        group.MapPost("/documents", (PostStockRequest request, IInventoryAccess service, CancellationToken ct) => service.PostAsync(request, ct));
        group.MapPost("/documents/{id:guid}/reverse", (Guid id, ReverseStockRequest request, IInventoryAccess service, CancellationToken ct) => service.ReverseAsync(id, request, ct));
    }
}

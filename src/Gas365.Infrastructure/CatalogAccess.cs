using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed class CatalogAccess(Gas365DbContext db, TenantScope scope) : ICatalogAccess
{
    private void Require(string permission)
    { if (!scope.Has(permission)) throw new AccessException(403, "You do not have permission for this action."); }
    private void Read() { if (!scope.Has(Permissions.PurchaseView) && !scope.Has(Permissions.PriceManage) && !scope.Has(Permissions.SalesDraft)) Require(Permissions.InventoryView); }
    private void Audit(string action, Guid id) => db.AuditEvents.Add(new AuditEvent
    { TenantId = scope.TenantId, ActorId = scope.UserId, Action = action, SubjectId = id });
    internal static string Text(string? value, int length = 120) => IdentityAccess.Text(value!, length);
    internal static string OptionalText(string? value, int length = 120)
    { return string.IsNullOrWhiteSpace(value) ? "" : Text(value, length); }
    internal static void Decimal(decimal value, int places, decimal maximum = 999999999m)
    {
        if (value < 0 || value > maximum || decimal.Round(value, places) != value)
            throw new AccessException(400, $"Use a non-negative value with at most {places} decimal places within the supported range.");
    }
    private IQueryable<Warehouse> VisibleWarehouses() => db.Warehouses.Where(w => scope.IsOwner || scope.Has(Permissions.BranchManage) ||
        db.BranchAccess.Any(a => a.MembershipId == scope.MembershipId && a.BranchId == w.BranchId));
    internal static ItemDto Dto(StockItem x) => new(x.Id, x.Sku, x.Name, x.Kind, x.UnitId, x.CategoryId,
        x.Brand, x.Classification, x.CapacityKg, x.TareWeightKg, x.SerialTracked, x.ReorderLevel, x.Active, x.Version);
    private static WarehouseDto Dto(Warehouse x) => new(x.Id, x.BranchId, x.Name, x.Location, x.Active, x.Version);
    public async Task<UnitDto[]> UnitsAsync(CancellationToken ct)
    { Read(); return await db.StockUnits.OrderBy(x => x.Code).Select(x => new UnitDto(x.Id, x.Code, x.DecimalPlaces)).ToArrayAsync(ct); }
    public async Task<UnitDto> CreateUnitAsync(UnitRequest request, CancellationToken ct)
    {
        Require(Permissions.CatalogManage);
        if (request.DecimalPlaces is < 0 or > 3) throw new AccessException(400, "Unit precision must be between 0 and 3 decimal places.");
        var row = new StockUnit { TenantId = scope.TenantId, Code = Text(request.Code, 20).ToUpperInvariant(), DecimalPlaces = request.DecimalPlaces };
        db.StockUnits.Add(row); Audit("unit.created", row.Id); await db.SaveChangesAsync(ct);
        return new(row.Id, row.Code, row.DecimalPlaces);
    }
    public async Task<CategoryDto[]> CategoriesAsync(CancellationToken ct)
    { Read(); return await db.StockCategories.OrderBy(x => x.Name).Select(x => new CategoryDto(x.Id, x.Name)).ToArrayAsync(ct); }
    public async Task<CategoryDto> CreateCategoryAsync(CategoryRequest request, CancellationToken ct)
    {
        Require(Permissions.CatalogManage);
        var row = new StockCategory { TenantId = scope.TenantId, Name = Text(request.Name) };
        db.StockCategories.Add(row); Audit("category.created", row.Id); await db.SaveChangesAsync(ct); return new(row.Id, row.Name);
    }
    public async Task<PageDto<ItemDto>> ItemsAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        Read(); if (page < 1 || page > 1000000 || pageSize is < 1 or > 100) throw new AccessException(400, "Invalid page range.");
        var query = db.StockItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = Text(search).ToUpperInvariant(); query = query.Where(x => x.Sku.Contains(term) || x.Name.ToUpper().Contains(term)); }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Sku).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(Dto).ToArray(), count, page, pageSize);
    }
    public async Task<ItemDto> ItemAsync(Guid id, CancellationToken ct)
    { Read(); return Dto(await db.StockItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Item not found.")); }
    public async Task<ItemDto> SaveItemAsync(Guid? id, ItemRequest request, CancellationToken ct)
    {
        Require(Permissions.CatalogManage);
        var unit = await db.StockUnits.SingleOrDefaultAsync(x => x.Id == request.UnitId, ct) ?? throw new AccessException(404, "Unit not found.");
        if (request.CategoryId.HasValue && !await db.StockCategories.AnyAsync(x => x.Id == request.CategoryId, ct)) throw new AccessException(404, "Category not found.");
        if (request.Kind is not ("Lpg" or "Cylinder" or "SparePart")) throw new AccessException(400, "Choose LPG, cylinder or spare part.");
        if (request.Kind == "SparePart" && request.CategoryId is null) throw new AccessException(400, "Spare parts require a category.");
        if (request.Kind != "Cylinder" && request.SerialTracked) throw new AccessException(400, "Only cylinders support serial tracking.");
        if (request.Kind == "Cylinder" && unit.DecimalPlaces != 0) throw new AccessException(400, "Cylinders require a whole-number unit.");
        if (request.Kind is "Lpg" or "Cylinder")
        {
            if (request.CapacityKg is null or <= 0 || request.Classification is not ("Domestic" or "Commercial"))
                throw new AccessException(400, "LPG and cylinders require a positive nominal capacity and Domestic or Commercial classification.");
        }
        if (request.CapacityKg.HasValue) Decimal(request.CapacityKg.Value, 3);
        if (request.TareWeightKg.HasValue) Decimal(request.TareWeightKg.Value, 3);
        Decimal(request.ReorderLevel, unit.DecimalPlaces);
        var row = id.HasValue ? await db.StockItems.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Item not found.") : new StockItem { TenantId = scope.TenantId };
        if (id.HasValue && row.Version != request.Version) throw new AccessException(409, "This item changed. Refresh and try again.");
        // Identity and stock interpretation stay stable after creation; correction uses a new SKU.
        if (id.HasValue && (row.Sku != Text(request.Sku, 40).ToUpperInvariant() || row.Kind != request.Kind || row.UnitId != request.UnitId || row.SerialTracked != request.SerialTracked))
            throw new AccessException(409, "SKU, kind, unit and serial policy cannot change. Create a new item.");
        row.Sku = Text(request.Sku, 40).ToUpperInvariant(); row.Name = Text(request.Name); row.Kind = request.Kind;
        row.UnitId = request.UnitId; row.CategoryId = request.CategoryId; row.Brand = OptionalText(request.Brand);
        row.Classification = OptionalText(request.Classification, 20); row.CapacityKg = request.CapacityKg; row.TareWeightKg = request.TareWeightKg;
        row.SerialTracked = request.SerialTracked; row.ReorderLevel = request.ReorderLevel; row.Active = request.Active;
        if (!id.HasValue) db.StockItems.Add(row);
        Audit(id.HasValue ? "item.updated" : "item.created", row.Id); await db.SaveChangesAsync(ct); return Dto(row);
    }
    public async Task<WarehouseDto[]> WarehousesAsync(CancellationToken ct)
    { Read(); return (await VisibleWarehouses().AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(ct)).Select(Dto).ToArray(); }
    public async Task<WarehouseDto> SaveWarehouseAsync(Guid? id, WarehouseRequest request, CancellationToken ct)
    {
        Require(Permissions.CatalogManage);
        if (!await db.Branches.AnyAsync(x => x.Id == request.BranchId && x.Active && (scope.IsOwner || scope.Has(Permissions.BranchManage) ||
            db.BranchAccess.Any(a => a.MembershipId == scope.MembershipId && a.BranchId == x.Id)), ct)) throw new AccessException(404, "Active branch not found.");
        var row = id.HasValue ? await VisibleWarehouses().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Warehouse not found.") : new Warehouse { TenantId = scope.TenantId, BranchId = request.BranchId };
        if (id.HasValue && row.Version != request.Version) throw new AccessException(409, "This warehouse changed. Refresh and try again.");
        if (row.BranchId != request.BranchId) throw new AccessException(409, "A warehouse cannot change branches.");
        row.Name = Text(request.Name); row.Location = OptionalText(request.Location, 200); row.Active = request.Active;
        if (!id.HasValue) db.Warehouses.Add(row);
        Audit(id.HasValue ? "warehouse.updated" : "warehouse.created", row.Id); await db.SaveChangesAsync(ct); return Dto(row);
    }
}

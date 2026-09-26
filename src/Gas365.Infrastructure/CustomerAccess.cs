using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed class CustomerAccess(Gas365DbContext db, TenantScope scope) : ICustomerAccess
{
    private void Require(string permission)
    { if (!scope.Has(Permissions.CustomerView) || !scope.Has(permission)) throw new AccessException(403, "You do not have permission for this customer or price-list action."); }
    private static void Page(int page)
    { if (page is < 1 or > 1000000) throw new AccessException(400, "Invalid page."); }
    private void Audit(string action, Guid id) => db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId, ActorId = scope.UserId, Action = action, SubjectId = id });
    private static CustomerDto Dto(Customer row) => new(row.Id, row.Name, row.Phone, row.Address, row.Active, row.Version);
    private static PriceListDto Dto(PriceList row) => new(row.Id, row.Name, row.Active, row.Version);
    public async Task<PageDto<CustomerDto>> CustomersAsync(string? search, int page, CancellationToken ct)
    {
        Require(Permissions.CustomerView); Page(page);
        var term = CatalogAccess.OptionalText(search).ToUpperInvariant();
        var query = db.Customers.AsNoTracking().Where(x => term == "" || x.Name.ToUpper().Contains(term) || x.Phone.Contains(term));
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20).ToArrayAsync(ct);
        return new(rows.Select(Dto).ToArray(), count, page, 20);
    }
    public async Task<CustomerDto> CustomerAsync(Guid id, CancellationToken ct)
    {
        Require(Permissions.CustomerView);
        return Dto(await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Customer not found."));
    }
    public async Task<CustomerDto> SaveCustomerAsync(Guid? id, CustomerRequest r, CancellationToken ct)
    {
        Require(Permissions.CustomerManage);
        var row = id.HasValue ? await db.Customers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Customer not found.") : new Customer { TenantId = scope.TenantId };
        if (id.HasValue && row.Version != r.Version) throw new AccessException(409, "Customer changed. Refresh before saving.");
        row.Name = CatalogAccess.Text(r.Name); row.Phone = CatalogAccess.OptionalText(r.Phone, 80); row.Address = CatalogAccess.OptionalText(r.Address, 300); row.Active = r.Active;
        if (!id.HasValue) db.Customers.Add(row);
        Audit(id.HasValue ? "customer.updated" : "customer.created", row.Id);
        await db.SaveChangesAsync(ct); return Dto(row);
    }
    public async Task<PageDto<PriceListDto>> PriceListsAsync(string? search, int page, CancellationToken ct)
    {
        Require(Permissions.CustomerView); Page(page);
        var term = CatalogAccess.OptionalText(search).ToUpperInvariant();
        var query = db.PriceLists.AsNoTracking().Where(x => term == "" || x.Name.ToUpper().Contains(term));
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20).ToArrayAsync(ct);
        return new(rows.Select(Dto).ToArray(), count, page, 20);
    }
    public async Task<PriceListDto> SavePriceListAsync(Guid? id, PriceListRequest r, CancellationToken ct)
    {
        Require(Permissions.PriceManage);
        var row = id.HasValue ? await db.PriceLists.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Price list not found.") : new PriceList { TenantId = scope.TenantId };
        if (id.HasValue && row.Version != r.Version) throw new AccessException(409, "Price list changed. Refresh before saving.");
        row.Name = CatalogAccess.Text(r.Name); row.Active = r.Active;
        if (!id.HasValue) db.PriceLists.Add(row);
        Audit(id.HasValue ? "price_list.updated" : "price_list.created", row.Id);
        await db.SaveChangesAsync(ct); return Dto(row);
    }
    private async Task<SalePriceDto[]> ReadPricesAsync(Guid listId, Guid? id, int skip, int take, CancellationToken ct)
    {
        var query = from p in db.SalePrices.AsNoTracking()
                    join i in db.StockItems on new { p.TenantId, Id = p.ItemId } equals new { i.TenantId, i.Id }
                    join u in db.StockUnits on new { i.TenantId, Id = i.UnitId } equals new { u.TenantId, u.Id }
                    where p.PriceListId == listId && (!id.HasValue || p.Id == id)
                    select new { Price = p, Item = i, Unit = u };
        return await query.OrderBy(x => x.Item.Sku).ThenBy(x => x.Price.Mode).ThenBy(x => x.Price.Id)
            .Skip(skip).Take(take)
            .Select(x => new SalePriceDto(x.Price.Id, x.Price.PriceListId, x.Price.ItemId, x.Item.Sku, x.Item.Name, x.Unit.Code, x.Price.Mode, x.Price.UnitPrice, x.Price.Active, x.Price.Version)).ToArrayAsync(ct);
    }
    public async Task<PageDto<SalePriceDto>> PricesAsync(Guid listId, int page, CancellationToken ct)
    {
        Require(Permissions.CustomerView); Page(page);
        if (!await db.PriceLists.AnyAsync(x => x.Id == listId, ct)) throw new AccessException(404, "Price list not found.");
        var count = await db.SalePrices.CountAsync(x => x.PriceListId == listId, ct);
        return new(await ReadPricesAsync(listId, null, (page - 1) * 20, 20, ct), count, page, 20);
    }
    public async Task<SalePriceDto> SavePriceAsync(Guid listId, Guid? id, SalePriceRequest r, CancellationToken ct)
    {
        Require(Permissions.PriceManage);
        if (!await db.PriceLists.AnyAsync(x => x.Id == listId && x.Active, ct)) throw new AccessException(404, "Active price list not found.");
        var item = await db.StockItems.SingleOrDefaultAsync(x => x.Id == r.ItemId && x.Active, ct) ?? throw new AccessException(404, "Active item not found.");
        if (!SaleModes.Supports(item.Kind, r.Mode)) throw new AccessException(400, "This sale mode does not support the selected item kind.");
        CatalogAccess.Decimal(r.UnitPrice, 6, 1000000m);
        var row = id.HasValue ? await db.SalePrices.SingleOrDefaultAsync(x => x.Id == id && x.PriceListId == listId, ct) ?? throw new AccessException(404, "Price not found.") : new SalePrice { TenantId = scope.TenantId, PriceListId = listId };
        if (id.HasValue && row.Version != r.Version) throw new AccessException(409, "Price changed. Refresh before saving.");
        if (id.HasValue && (row.ItemId != r.ItemId || row.Mode != r.Mode)) throw new AccessException(400, "Deactivate this price and add another to change its item or sale mode.");
        row.ItemId = r.ItemId; row.Mode = r.Mode; row.UnitPrice = r.UnitPrice; row.Active = r.Active;
        if (!id.HasValue) db.SalePrices.Add(row);
        Audit(id.HasValue ? "sale_price.updated" : "sale_price.created", row.Id);
        await db.SaveChangesAsync(ct); return (await ReadPricesAsync(listId, row.Id, 0, 1, ct)).Single();
    }
}

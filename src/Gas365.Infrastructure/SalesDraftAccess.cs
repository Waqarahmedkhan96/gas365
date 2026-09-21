using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed class SalesDraftAccess(Gas365DbContext db, TenantScope scope) : ISalesDraftAccess
{
    private void Require(string permission)
    { if (!scope.Has(Permissions.SalesView) || !scope.Has(permission)) throw new AccessException(403, "You do not have permission for this sales action."); }
    private async Task BranchAsync(Guid id, bool active, CancellationToken ct)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == id && (!active || b.Active) && (scope.IsOwner || scope.Has(Permissions.BranchManage) || db.BranchAccess.Any(a => a.MembershipId == scope.MembershipId && a.BranchId == b.Id)), ct))
            throw new AccessException(404, "Branch not found.");
    }
    private static SaleDraftDto Dto(SaleDraft d) => new(d.Id, d.BranchId, d.CustomerId, d.PriceListId, d.Reference, d.Version, JsonSerializer.Deserialize<SaleDraftSnapshot>(d.Snapshot)!);
    public async Task<PageDto<SaleDraftDto>> DraftsAsync(Guid branchId, int page, CancellationToken ct)
    {
        Require(Permissions.SalesView); await BranchAsync(branchId, false, ct);
        if (page is < 1 or > 1000000) throw new AccessException(400, "Invalid page.");
        var q = db.SaleDrafts.AsNoTracking().Where(x => x.BranchId == branchId); var count = await q.CountAsync(ct);
        var rows = await q.OrderBy(x => x.Reference).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20).ToArrayAsync(ct);
        return new(rows.Select(Dto).ToArray(), count, page, 20);
    }
    public async Task<SaleDraftDto> DraftAsync(Guid id, CancellationToken ct)
    {
        Require(Permissions.SalesView);
        var row = await db.SaleDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Sales draft not found.");
        await BranchAsync(row.BranchId, false, ct); return Dto(row);
    }
    public async Task<SaleDraftDto> SaveAsync(Guid? id, SaleDraftRequest r, CancellationToken ct)
    {
        Require(Permissions.SalesDraft); await BranchAsync(r.BranchId, true, ct);
        if (r.Lines is null || (r.Lines.Length is < 1 or > 100) || r.Lines.Any(x => x is null)) throw new AccessException(400, "Provide 1–100 sale lines.");
        if (r.Lines.Any(x => x.Discount != 0)) Require(Permissions.SalesDiscount);
        // One transaction gives the draft a consistent price snapshot; no stock is reserved.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var row = id.HasValue ? await db.SaleDrafts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Sales draft not found.") : new SaleDraft { TenantId = scope.TenantId };
        if (id.HasValue)
        {
            await BranchAsync(row.BranchId, true, ct);
            if (row.Version != r.Version) throw new AccessException(409, "Sales draft changed. Refresh before saving.");
        }
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == r.CustomerId && x.Active, ct) ?? throw new AccessException(404, "Active customer not found.");
        var list = await db.PriceLists.SingleOrDefaultAsync(x => x.Id == r.PriceListId && x.Active, ct) ?? throw new AccessException(404, "Active price list not found.");
        var lines = new List<SaleLineDto>();
        foreach (var line in r.Lines)
        {
            var price = await db.SalePrices.SingleOrDefaultAsync(x => x.Id == line.PriceId && x.PriceListId == list.Id && x.Active, ct) ?? throw new AccessException(404, "Active selling price not found.");
            if (price.Version != line.PriceVersion) throw new AccessException(409, "A selling price changed. Review current prices before saving.");
            var item = await db.StockItems.SingleOrDefaultAsync(x => x.Id == price.ItemId && x.Active, ct) ?? throw new AccessException(404, "Active item not found.");
            var unit = await db.StockUnits.SingleAsync(x => x.Id == item.UnitId, ct);
            var warehouse = await db.Warehouses.SingleOrDefaultAsync(x => x.Id == line.WarehouseId && x.BranchId == r.BranchId && x.Active, ct) ?? throw new AccessException(404, "Active warehouse in this branch not found.");
            CatalogAccess.Decimal(line.Quantity, unit.DecimalPlaces, 1000000m);
            SaleAmounts amounts;
            try { amounts = SaleCalculation.Calculate(line.Quantity, price.UnitPrice, line.Discount); }
            catch (ArgumentException e) { throw new AccessException(400, e.Message); }
            lines.Add(new(price.Id, price.Version, item.Id, item.Sku, item.Name, price.Mode, unit.Code, warehouse.Id, warehouse.Name, line.Quantity, price.UnitPrice, amounts.Gross, amounts.Discount, amounts.Net));
        }
        row.BranchId = r.BranchId; row.CustomerId = customer.Id; row.PriceListId = list.Id; row.Reference = CatalogAccess.Text(r.Reference, 120);
        row.Content = JsonSerializer.Serialize(r);
        row.Snapshot = JsonSerializer.Serialize(new SaleDraftSnapshot(customer.Name, list.Name, CatalogAccess.OptionalText(r.Notes, 300), lines.ToArray(), lines.Sum(x => x.Gross), lines.Sum(x => x.Discount), lines.Sum(x => x.Net)));
        if (!id.HasValue) db.SaleDrafts.Add(row);
        db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId, ActorId = scope.UserId, SubjectId = row.Id, Action = "sale.draft_saved" });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Dto(row);
    }
}

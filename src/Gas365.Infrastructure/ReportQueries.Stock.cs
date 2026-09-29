using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class ReportAccess
{
    private sealed record ItemInfo(Guid Id, string Sku, string Name, string Kind, string Unit);

    private async Task<Dictionary<Guid, ItemInfo>> ItemsAsync(CancellationToken ct) =>
        await (from i in db.StockItems.AsNoTracking()
               join u in db.StockUnits.AsNoTracking() on i.UnitId equals u.Id
               select new ItemInfo(i.Id, i.Sku, i.Name, i.Kind, u.Code)).ToDictionaryAsync(x => x.Id, ct);

    // Stock is dated by its document's business date; movements themselves carry no date.
    private IQueryable<StockMovement> Movements(Guid[] ids, DateOnly through) =>
        from m in db.StockMovements.AsNoTracking()
        join d in db.StockDocuments.AsNoTracking() on m.DocumentId equals d.Id
        where ids.Contains(m.BranchId) && d.BusinessDate <= through
        select m;

    private async Task<(ReportColumn[], List<object?[]>, object?[])> StockOnHandAsync(Guid[] ids, Dictionary<Guid, string> names, ReportFilter f, CancellationToken ct)
    {
        var items = await ItemsAsync(ct);
        var warehouses = await db.Warehouses.AsNoTracking().Where(x => ids.Contains(x.BranchId)).ToDictionaryAsync(x => x.Id, ct);
        var balances = await Movements(ids, f.To).GroupBy(x => new { x.WarehouseId, x.ItemId, x.Bucket })
            .Select(g => new { g.Key.WarehouseId, g.Key.ItemId, g.Key.Bucket, Quantity = g.Sum(x => x.Quantity), Value = g.Sum(x => x.Value) }).ToArrayAsync(ct);
        ReportColumn[] columns = [Text("branch", "Branch"), Text("warehouse", "Warehouse"), Text("sku", "SKU"), Text("item", "Item"), Text("kind", "Type"),
            Text("state", "Stock state"), new("quantity", "Quantity", "quantity"), Text("unit", "Unit"), Money("value", "Value")];
        var rows = balances.Where(x => (x.Quantity != 0 || x.Value != 0) && (f.Type is null || items[x.ItemId].Kind == f.Type))
            .Select(x => new { Row = x, Item = items[x.ItemId], Warehouse = warehouses[x.WarehouseId] })
            .OrderBy(x => names[x.Warehouse.BranchId]).ThenBy(x => x.Warehouse.Name).ThenBy(x => x.Item.Sku).ThenBy(x => x.Row.Bucket)
            .Select(x => new object?[] { names[x.Warehouse.BranchId], x.Warehouse.Name, x.Item.Sku, x.Item.Name, x.Item.Kind, x.Row.Bucket, x.Row.Quantity, x.Item.Unit, x.Row.Value }).ToList();
        return (columns, rows, Sum(columns, rows, "value"));
    }

    private async Task<(ReportColumn[], List<object?[]>, object?[])> StockMovementAsync(Guid[] ids, Dictionary<Guid, string> names, ReportFilter f, CancellationToken ct)
    {
        var items = await ItemsAsync(ct);
        var warehouses = await db.Warehouses.AsNoTracking().Where(x => ids.Contains(x.BranchId)).ToDictionaryAsync(x => x.Id, ct);
        var opening = await Movements(ids, f.From.AddDays(-1)).GroupBy(x => new { x.WarehouseId, x.ItemId })
            .Select(g => new { g.Key.WarehouseId, g.Key.ItemId, Quantity = g.Sum(x => x.Quantity), Value = g.Sum(x => x.Value) }).ToArrayAsync(ct);
        // Net per document first so state changes inside one document (for example Full to CustomerHeld) cancel out.
        var period = await (from m in db.StockMovements.AsNoTracking()
                            join d in db.StockDocuments.AsNoTracking() on m.DocumentId equals d.Id
                            where ids.Contains(m.BranchId) && d.BusinessDate >= f.From && d.BusinessDate <= f.To
                            group m by new { m.DocumentId, m.WarehouseId, m.ItemId } into g
                            select new { g.Key.WarehouseId, g.Key.ItemId, Quantity = g.Sum(x => x.Quantity), Value = g.Sum(x => x.Value) }).ToArrayAsync(ct);
        var keys = opening.Select(x => (x.WarehouseId, x.ItemId)).Concat(period.Select(x => (x.WarehouseId, x.ItemId))).Distinct();
        ReportColumn[] columns = [Text("branch", "Branch"), Text("warehouse", "Warehouse"), Text("sku", "SKU"), Text("item", "Item"), Text("unit", "Unit"),
            new("opening", "Opening quantity", "quantity"), new("received", "Received", "quantity"), new("issued", "Issued", "quantity"), new("closing", "Closing quantity", "quantity"),
            Money("openingValue", "Opening value"), Money("valueChange", "Value change"), Money("closingValue", "Closing value")];
        var rows = keys.Where(k => f.Type is null || items[k.ItemId].Kind == f.Type).Select(k =>
        {
            var o = opening.Where(x => x.WarehouseId == k.WarehouseId && x.ItemId == k.ItemId).ToArray();
            var p = period.Where(x => x.WarehouseId == k.WarehouseId && x.ItemId == k.ItemId).ToArray();
            decimal openQty = o.Sum(x => x.Quantity), openValue = o.Sum(x => x.Value), received = p.Where(x => x.Quantity > 0).Sum(x => x.Quantity), issued = -p.Where(x => x.Quantity < 0).Sum(x => x.Quantity), change = p.Sum(x => x.Value);
            var w = warehouses[k.WarehouseId]; var i = items[k.ItemId];
            return new { Branch = names[w.BranchId], Warehouse = w.Name, i.Sku, Cells = new object?[] { names[w.BranchId], w.Name, i.Sku, i.Name, i.Unit, openQty, received, issued, openQty + received - issued, openValue, change, openValue + change } };
        }).Where(x => x.Cells.Skip(5).Any(c => (decimal)c! != 0)).OrderBy(x => x.Branch).ThenBy(x => x.Warehouse).ThenBy(x => x.Sku).Select(x => x.Cells).ToList();
        return (columns, rows, Sum(columns, rows, "openingValue", "valueChange", "closingValue"));
    }

    private async Task<(ReportColumn[], List<object?[]>, object?[])> ValuationAsync(Guid[] ids, ReportFilter f, CancellationToken ct)
    {
        var items = await ItemsAsync(ct);
        var balances = await Movements(ids, f.To).GroupBy(x => x.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(x => x.Quantity), Value = g.Sum(x => x.Value) }).ToArrayAsync(ct);
        ReportColumn[] columns = [Text("sku", "SKU"), Text("item", "Item"), Text("kind", "Type"), Text("unit", "Unit"), new("quantity", "Quantity", "quantity"), Money("value", "Value"), new("average", "Average cost", "cost")];
        var rows = balances.Where(x => (x.Quantity != 0 || x.Value != 0) && (f.Type is null || items[x.ItemId].Kind == f.Type)).OrderBy(x => items[x.ItemId].Sku)
            .Select(x => new object?[] { items[x.ItemId].Sku, items[x.ItemId].Name, items[x.ItemId].Kind, items[x.ItemId].Unit, x.Quantity, x.Value,
                x.Quantity == 0 ? null : Math.Round(x.Value / x.Quantity, 6, MidpointRounding.AwayFromZero) }).ToList();
        return (columns, rows, Sum(columns, rows, "value"));
    }

    private async Task<(ReportColumn[], List<object?[]>, object?[])> CustodyAsync(Guid[] ids, ReportFilter f, CancellationToken ct)
    {
        var items = await ItemsAsync(ct);
        var loans = await db.CylinderLoans.AsNoTracking().Where(x => ids.Contains(x.BranchId)).Select(x => new { x.Id, x.CustomerId, x.ItemId }).ToArrayAsync(ct);
        var loanIds = loans.Select(x => x.Id).ToArray();
        var entries = await db.CylinderLoanEntries.AsNoTracking().Where(x => loanIds.Contains(x.LoanId) && x.BusinessDate <= f.To)
            .Select(x => new { x.Id, x.LoanId, x.Kind, x.ReversesId, x.Liability, x.Receivable }).ToArrayAsync(ct);
        var customers = await db.Customers.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var byLoan = entries.ToLookup(x => x.LoanId);
        var rows = loans.Select(l =>
        {
            var e = byLoan[l.Id].ToArray(); var reversed = e.Select(x => x.ReversesId).ToHashSet();
            var onLoan = e.Any(x => x.Kind == "Issue") && !e.Any(x => x.Kind == "IssueReversal") && !e.Any(x => x.Kind == "Return" && !reversed.Contains(x.Id));
            return new { l.CustomerId, l.ItemId, OnLoan = onLoan ? 1 : 0, Deposit = e.Sum(x => x.Liability), Due = e.Sum(x => x.Receivable) };
        }).GroupBy(x => new { x.CustomerId, x.ItemId })
          .Select(g => new { Customer = customers.GetValueOrDefault(g.Key.CustomerId, "Unknown customer"), Item = items[g.Key.ItemId].Name, OnLoan = g.Sum(x => x.OnLoan), Deposit = g.Sum(x => x.Deposit), Due = g.Sum(x => x.Due) })
          .Where(x => x.OnLoan != 0 || x.Deposit != 0 || x.Due != 0).OrderBy(x => x.Customer).ThenBy(x => x.Item)
          .Select(x => new object?[] { x.Customer, x.Item, x.OnLoan, x.Deposit, x.Due }).ToList();
        ReportColumn[] columns = [Text("customer", "Customer"), Text("cylinder", "Cylinder type"), new("onLoan", "On loan", "count"), Money("deposits", "Deposits held"), Money("due", "Amount due")];
        return (columns, rows, Sum(columns, rows, "onLoan", "deposits", "due"));
    }
}

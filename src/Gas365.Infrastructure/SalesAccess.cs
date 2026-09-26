using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed class SalesAccess(Gas365DbContext db, TenantScope scope, IInventoryCosting costing) : ISalesAccess
{
    public async Task<byte[]> ReceiptAsync(Guid id, CancellationToken ct)
    {
        var sale = await SaleAsync(id, ct); var bytes = SaleReceipt.Render(sale);
        Audit("sale.receipt_exported", sale.Id); await db.SaveChangesAsync(ct); return bytes;
    }
    private void Require(string permission)
    { if (!scope.Has(Permissions.SalesView) || !scope.Has(permission)) throw new AccessException(403, "You do not have permission for this sales action."); }
    private async Task BranchAsync(Guid id, CancellationToken ct, bool active = false)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == id && (!active || b.Active) &&
            (scope.IsOwner || scope.Has(Permissions.BranchManage) || db.BranchAccess.Any(a => a.MembershipId == scope.MembershipId && a.BranchId == b.Id)), ct))
            throw new AccessException(404, "Branch not found.");
    }
    private async Task LockAsync(CancellationToken ct) => await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Tenants\" WHERE \"TenantId\" = {scope.TenantId} FOR UPDATE", ct);
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static T Parse<T>(string value) => JsonSerializer.Deserialize<T>(value)!;
    private void Audit(string action, Guid id) => db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId, ActorId = scope.UserId, SubjectId = id, Action = action });
    private async Task<SaleEntry?> ReplayAsync(string key, string hash, CancellationToken ct)
    {
        if (CatalogAccess.Text(key, 80) != key || key.StartsWith("internal:", StringComparison.Ordinal)) throw new AccessException(400, "Invalid command key.");
        var prior = await db.SaleEntries.SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (prior is not null && prior.RequestHash != hash) throw new AccessException(409, "Command key was used with different contents.");
        return prior;
    }
    private async Task<SaleEntry> EntryAsync(SaleDocument doc, string kind, decimal receivable, decimal cash, decimal revenue, decimal cogs,
        string key, string hash, string reason, CancellationToken ct, Guid? reverses = null)
    {
        var now = DateTimeOffset.UtcNow; var tenant = await db.Tenants.SingleAsync(ct);
        var entry = new SaleEntry
        {
            TenantId = scope.TenantId,
            BranchId = doc.BranchId,
            SaleId = doc.Id,
            ActorId = scope.UserId,
            Kind = kind,
            Receivable = receivable,
            Cash = cash,
            Revenue = revenue,
            Cogs = cogs,
            IdempotencyKey = key,
            RequestHash = hash,
            Reason = CatalogAccess.Text(reason, 300),
            PostedAt = now,
            BusinessDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone)).DateTime),
            ReversesId = reverses
        };
        db.SaleEntries.Add(entry); Audit("sale." + kind.ToLowerInvariant(), entry.Id); return entry;
    }
    public async Task<SaleDto> SaleAsync(Guid id, CancellationToken ct)
    {
        Require(Permissions.SalesView);
        var doc = await db.SaleDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id || x.DraftId == id, ct) ?? throw new AccessException(404, "Posted sale not found.");
        await BranchAsync(doc.BranchId, ct);
        var entries = await db.SaleEntries.AsNoTracking().Where(x => x.SaleId == doc.Id).OrderBy(x => x.Sequence).ToArrayAsync(ct);
        var reversed = entries.Any(x => x.Kind == "Reversal"); var outstanding = entries.Sum(x => x.Receivable); var paid = entries.Sum(x => x.Cash);
        var profit = scope.Has(Permissions.SalesProfit);
        var loans = await db.CylinderLoans.Where(x => x.SaleId == doc.Id).Select(x => x.Id).ToArrayAsync(ct);
        var depositCollected = await db.CylinderLoanEntries.Where(x => loans.Contains(x.LoanId) && (x.Kind == "Issue" || x.Kind == "IssueReversal")).SumAsync(x => x.Cash, ct);
        return new(doc.Id, doc.DraftId, doc.BranchId, doc.CustomerId, doc.Reference,
            reversed ? "Reversed" : outstanding == 0 ? "Paid" : paid > 0 ? "Partially Paid" : "Posted",
            doc.Total, paid, outstanding, profit ? doc.Cogs : null, profit ? doc.Total - doc.Cogs : null, doc.StockDocumentId,
            Parse<SaleDraftSnapshot>(doc.Content), entries.Select(e => new SaleEntryDto(e.Id, e.Kind, e.Receivable, e.Cash, e.Revenue,
                profit ? e.Cogs : null, e.Reason, e.ActorId, e.PostedAt, e.BusinessDate, e.ReversesId)).ToArray(), depositCollected, loans);
    }
    public async Task<PageDto<SaleDto>> SalesAsync(Guid branchId, Guid? customerId, int page, CancellationToken ct)
    {
        Require(Permissions.SalesView); await BranchAsync(branchId, ct);
        if (page is < 1 or > 1000000) throw new AccessException(400, "Invalid page.");
        if (customerId.HasValue && !await db.Customers.AnyAsync(x => x.Id == customerId, ct)) throw new AccessException(404, "Customer not found.");
        var q = db.SaleDocuments.Where(x => x.BranchId == branchId && (!customerId.HasValue || x.CustomerId == customerId));
        var count = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(x => x.Id).Skip((page - 1) * 20).Take(20).Select(x => x.Id).ToArrayAsync(ct);
        var rows = new List<SaleDto>(); foreach (var id in ids) rows.Add(await SaleAsync(id, ct));
        return new(rows.ToArray(), count, page, 20);
    }
    public async Task<SaleDto> PostAsync(Guid id, SalePostRequest r, CancellationToken ct)
    {
        Require(Permissions.SalesPost); if (r.CashPaid != 0) Require(Permissions.SalesPay);
        var hash = Hash(new { Action = "post", id, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var replay = await ReplayAsync(r.IdempotencyKey, hash, ct); if (replay is not null) return await SaleAsync(replay.SaleId, ct);
        var draft = await db.SaleDrafts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Sales draft not found.");
        await BranchAsync(draft.BranchId, ct, true);
        if (draft.Version != r.Version || await db.SaleDocuments.AnyAsync(x => x.DraftId == id, ct)) throw new AccessException(409, "Draft changed or was posted.");
        var snapshot = Parse<SaleDraftSnapshot>(draft.Snapshot);
        if (!await db.Customers.AnyAsync(x => x.Id == draft.CustomerId && x.Active, ct) || !await db.PriceLists.AnyAsync(x => x.Id == draft.PriceListId && x.Active, ct))
            throw new AccessException(409, "Customer or price list is inactive.");
        if (snapshot.Discount > 0) Require(Permissions.SalesDiscount);
        if (r.Serials is null || r.Serials.Length != snapshot.Lines.Length || r.Serials.Any(x => x is null)) throw new AccessException(400, "Provide a serial array for each sale line.");
        if (r.Cylinders is not null && (r.Cylinders.Length != snapshot.Lines.Length || r.Cylinders.Any(x => x is null || x.IncomingSerials is null))) throw new AccessException(400, "Provide cylinder options for each line.");
        CatalogAccess.Decimal(r.CashPaid, 2, snapshot.Net);
        var doc = new SaleDocument { TenantId = scope.TenantId, BranchId = draft.BranchId, CustomerId = draft.CustomerId, DraftId = draft.Id, Reference = draft.Reference, Content = draft.Snapshot, Total = snapshot.Net };
        var invoice = await EntryAsync(doc, "Invoice", doc.Total, 0, doc.Total, 0, r.IdempotencyKey, hash, doc.Reference, ct);
        var stock = new StockDocument { TenantId = scope.TenantId, BranchId = doc.BranchId, ActorId = scope.UserId, Kind = "SaleIssue", Reason = doc.Reference, IdempotencyKey = "sale:" + doc.Id, RequestHash = hash, PostedAt = invoice.PostedAt, BusinessDate = invoice.BusinessDate };
        doc.StockDocumentId = stock.Id; db.StockDocuments.Add(stock);
        var loanService = new CylinderLoanAccess(db, scope, costing);
        var loans = new List<CylinderLoan>();
        // Persist movements inside the outer transaction so repeated item lines see earlier issues.
        // Add the immutable invoice/document only after their final COGS is known.
        db.Entry(invoice).State = EntityState.Detached;
        for (var i = 0; i < snapshot.Lines.Length; i++)
        {
            var line = snapshot.Lines[i];
            var price = await db.SalePrices.SingleOrDefaultAsync(x => x.Id == line.PriceId && x.PriceListId == draft.PriceListId && x.Active, ct);
            if (price is null || price.Version != line.PriceVersion || price.UnitPrice != line.UnitPrice || price.ItemId != line.ItemId || price.Mode != line.Mode)
                throw new AccessException(409, "A selling price changed. Review and save the draft again.");
            var item = await db.StockItems.SingleOrDefaultAsync(x => x.Id == line.ItemId && x.Active, ct) ?? throw new AccessException(409, "An item is inactive.");
            if (!await db.Warehouses.AnyAsync(x => x.Id == line.WarehouseId && x.BranchId == doc.BranchId && x.Active, ct)) throw new AccessException(409, "A warehouse is inactive.");
            var unit = await db.StockUnits.SingleAsync(x => x.Id == item.UnitId, ct);
            CatalogAccess.Decimal(line.Quantity, unit.DecimalPlaces, 1000000);
            CatalogAccess.Decimal(line.LossQuantity, unit.DecimalPlaces, 1000000);
            if (!SaleModes.Supports(item.Kind, line.Mode)) throw new AccessException(409, "Item mode changed.");
            var serials = r.Serials[i].Select(x => CatalogAccess.Text(x, 80).ToUpperInvariant()).ToArray();
            if (serials.Length > 1000) throw new AccessException(400, "At most 1,000 serials per line.");
            var options = r.Cylinders?[i];
            if (line.Mode != "LoanRefill" && options is { DepositPerCylinder: not 0 } || line.Mode != "Exchange" && options?.IncomingSerials.Length > 0)
                throw new AccessException(400, "Deposit or incoming serials do not apply to this sale mode.");
            if (line.Mode is "Refill" or "SparePart" or "CylinderOnly")
                doc.Cogs += await IssueAsync(stock, item, line.WarehouseId, line.Mode == "CylinderOnly" ? "Empty" : "Available", line.Quantity + line.LossQuantity, serials, null, null, ct);
            else
            {
                var policy = await db.CylinderPolicies.SingleOrDefaultAsync(x => x.ItemId == item.Id, ct);
                if (policy is null || policy.Version != line.CylinderPolicyVersion) throw new AccessException(409, "Cylinder policy changed. Review and save the draft again.");
                var gas = await db.StockItems.SingleOrDefaultAsync(x => x.Id == policy.GasItemId && x.Active && x.Kind == "Lpg", ct) ?? throw new AccessException(409, "Configured LPG item is inactive.");
                doc.Cogs += await IssueAsync(stock, gas, line.WarehouseId, "Available", line.Quantity * policy.GasQuantity, [], null, null, ct);
                var destination = line.Mode == "Exchange" ? "Empty" : line.Mode == "LoanRefill" ? "CustomerHeld" : null;
                var incoming = line.Mode == "Exchange" ? (options?.IncomingSerials ?? []).Select(x => CatalogAccess.Text(x, 80).ToUpperInvariant()).ToArray() : null;
                doc.Cogs += await IssueAsync(stock, item, line.WarehouseId, "Full", line.Quantity, serials, destination, incoming, ct);
                if (line.Mode == "LoanRefill")
                {
                    if (options is null) throw new AccessException(400, "Enter the cylinder deposit explicitly, including zero.");
                    CatalogAccess.Decimal(options.DepositPerCylinder, 2, 1000000000);
                    if (loans.Count + line.Quantity > 1000) throw new AccessException(400, "Issue at most 1,000 borrowed cylinders per sale.");
                    if (options.DepositPerCylinder > 0) Require(Permissions.SalesPay);
                    if (options.DepositPerCylinder < policy.SuggestedDeposit && !scope.Has(Permissions.CylinderWaive) &&
                        !(options.DepositPerCylinder == 0 && await db.CustomerDepositArrangements.AnyAsync(x => x.CustomerId == doc.CustomerId && x.ZeroDepositApproved, ct)))
                        throw new AccessException(403, "Reducing or waiving this deposit requires authorization.");
                    for (var n = 0; n < line.Quantity; n++) loans.Add(new CylinderLoan
                    {
                        TenantId = scope.TenantId,
                        BranchId = doc.BranchId,
                        SaleId = doc.Id,
                        CustomerId = doc.CustomerId,
                        ItemId = item.Id,
                        WarehouseId = line.WarehouseId,
                        Serial = item.SerialTracked ? serials[n] : "",
                        Deposit = options.DepositPerCylinder,
                        UsageTerms = policy.UsageTerms
                    });
                }
            }
        }
        invoice.Cogs = doc.Cogs; db.SaleDocuments.Add(doc); db.SaleEntries.Add(invoice);
        foreach (var loan in loans)
        {
            db.CylinderLoans.Add(loan); var entry = await loanService.EntryAsync(loan, "Issue", "internal:" + loan.Id, hash, "Deposit collected on cylinder issue", ct);
            entry.Cash = loan.Deposit; entry.Liability = loan.Deposit;
        }
        if (r.CashPaid > 0) await EntryAsync(doc, "Payment", -r.CashPaid, r.CashPaid, 0, 0, "internal:" + doc.Id, hash, "Cash received on posting", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await SaleAsync(doc.Id, ct);
    }
    private async Task<decimal> IssueAsync(StockDocument stock, StockItem item, Guid warehouseId, string bucket, decimal amount,
        string[] serials, string? destinationBucket, string[]? incomingSerials, CancellationToken ct)
    {
        var inventory = new InventoryAccess(db, scope, costing);
        await inventory.ValidateSerialsAsync(item, serials, amount, warehouseId, bucket, true, ct);
        if (incomingSerials is not null)
        {
            if (incomingSerials.Intersect(serials).Any()) throw new AccessException(400, "Exchange must receive a different cylinder serial.");
            await inventory.ValidateSerialsAsync(item, incomingSerials, amount, warehouseId, destinationBucket!, false, ct);
        }
        var pool = db.StockMovements.Where(x => x.ItemId == item.Id && x.WarehouseId == warehouseId);
        if (await pool.Where(x => x.Bucket == bucket).SumAsync(x => x.Quantity, ct) < amount) throw new AccessException(409, "Insufficient stock for the sale and recorded loss.");
        var quantity = await pool.SumAsync(x => x.Quantity, ct); var value = await pool.SumAsync(x => x.Value, ct);
        var cost = costing.IssueValue(quantity, value, amount); var unitCost = costing.Average(quantity, value);
        db.StockMovements.Add(new StockMovement { TenantId = scope.TenantId, BranchId = stock.BranchId, DocumentId = stock.Id, ItemId = item.Id, WarehouseId = warehouseId, Bucket = bucket, Quantity = -amount, Value = -cost, UnitCost = unitCost, Serials = serials });
        if (destinationBucket is not null) db.StockMovements.Add(new StockMovement { TenantId = scope.TenantId, BranchId = stock.BranchId, DocumentId = stock.Id, ItemId = item.Id, WarehouseId = warehouseId, Bucket = destinationBucket, Quantity = amount, Value = cost, UnitCost = unitCost, Serials = incomingSerials ?? serials });
        await db.SaveChangesAsync(ct); return destinationBucket is null ? cost : 0;
    }
    private async Task<SaleDocument> PostedAsync(Guid id, CancellationToken ct)
    {
        var doc = await db.SaleDocuments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Posted sale not found.");
        await BranchAsync(doc.BranchId, ct, true);
        if (await db.SaleEntries.AnyAsync(x => x.SaleId == id && x.Kind == "Reversal", ct)) throw new AccessException(409, "Sale was reversed.");
        return doc;
    }
    public async Task<SaleDto> PayAsync(Guid id, SalePaymentRequest r, CancellationToken ct)
    {
        Require(Permissions.SalesPay); var hash = Hash(new { Action = "pay", id, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var replay = await ReplayAsync(r.IdempotencyKey, hash, ct); if (replay is not null) return await SaleAsync(replay.SaleId, ct);
        var doc = await PostedAsync(id, ct); var entries = await db.SaleEntries.Where(x => x.SaleId == id).OrderBy(x => x.Sequence).ToArrayAsync(ct);
        if (entries[^1].Id != r.ExpectedLastEntryId) throw new AccessException(409, "Payments changed. Refresh before receiving payment.");
        CatalogAccess.Decimal(r.Amount, 2, entries.Sum(x => x.Receivable));
        if (r.Amount <= 0) throw new AccessException(400, "Payment must be positive and cannot exceed the balance.");
        await EntryAsync(doc, "Payment", -r.Amount, r.Amount, 0, 0, r.IdempotencyKey, hash, r.Reason, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await SaleAsync(id, ct);
    }
    public async Task<SaleDto> ReversePaymentAsync(Guid id, Guid entryId, SaleReverseRequest r, CancellationToken ct)
    {
        Require(Permissions.SalesReverse); Require(Permissions.SalesPay); var hash = Hash(new { Action = "reversePayment", id, entryId, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var replay = await ReplayAsync(r.IdempotencyKey, hash, ct); if (replay is not null) return await SaleAsync(replay.SaleId, ct);
        var doc = await PostedAsync(id, ct);
        var entry = await db.SaleEntries.SingleOrDefaultAsync(x => x.Id == entryId && x.SaleId == id && x.Kind == "Payment", ct) ?? throw new AccessException(404, "Payment not found.");
        if (await db.SaleEntries.AnyAsync(x => x.ReversesId == entryId, ct)) throw new AccessException(409, "Payment already reversed.");
        await EntryAsync(doc, "PaymentReversal", -entry.Receivable, -entry.Cash, 0, 0, r.IdempotencyKey, hash, r.Reason, ct, entry.Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await SaleAsync(id, ct);
    }
    public async Task<SaleDto> ReverseAsync(Guid id, SaleReverseRequest r, CancellationToken ct)
    {
        Require(Permissions.SalesReverse); var hash = Hash(new { Action = "reverse", id, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var replay = await ReplayAsync(r.IdempotencyKey, hash, ct); if (replay is not null) return await SaleAsync(replay.SaleId, ct);
        var doc = await PostedAsync(id, ct);
        if (await db.SaleEntries.Where(x => x.SaleId == id).SumAsync(x => x.Cash, ct) != 0) throw new AccessException(409, "Reverse recorded payments before reversing the sale.");
        var loans = await db.CylinderLoans.Where(x => x.SaleId == id).ToArrayAsync(ct);
        var loanIds = loans.Select(x => x.Id).ToArray();
        if (await db.CylinderLoanEntries.AnyAsync(x => loanIds.Contains(x.LoanId) && x.Kind != "Issue" && x.Kind != "Approval", ct)) throw new AccessException(409, "Cylinder return or settlement history prevents reversing the original sale.");
        if (loans.Any(x => x.Deposit > 0)) Require(Permissions.SalesPay);
        var rows = await db.StockMovements.Where(x => x.DocumentId == doc.StockDocumentId).ToArrayAsync(ct);
        foreach (var pool in rows.GroupBy(x => new { x.ItemId, x.WarehouseId }))
        {
            var last = pool.Max(x => x.Sequence);
            if (await db.StockMovements.AnyAsync(x => x.ItemId == pool.Key.ItemId && x.WarehouseId == pool.Key.WarehouseId && x.Sequence > last, ct)) throw new AccessException(409, "Later stock movements prevent full sale reversal.");
        }
        var serials = rows.SelectMany(x => x.Serials).ToArray(); var lastSequence = rows.Max(x => x.Sequence);
        if (serials.Length > 0 && await db.StockMovements.AnyAsync(x => x.Sequence > lastSequence && x.Serials.Any(s => serials.Contains(s)), ct)) throw new AccessException(409, "Later serial movements prevent reversal.");
        var invoice = await db.SaleEntries.SingleAsync(x => x.SaleId == id && x.Kind == "Invoice", ct);
        var reversal = await EntryAsync(doc, "Reversal", -doc.Total, 0, -doc.Total, -doc.Cogs, r.IdempotencyKey, hash, r.Reason, ct, invoice.Id);
        var stock = new StockDocument { TenantId = scope.TenantId, BranchId = doc.BranchId, ActorId = scope.UserId, Kind = "Reversal", Reason = r.Reason, IdempotencyKey = "sale-reversal:" + reversal.Id, RequestHash = hash, ReversesId = doc.StockDocumentId, PostedAt = reversal.PostedAt, BusinessDate = reversal.BusinessDate };
        db.StockDocuments.Add(stock);
        foreach (var m in rows) db.StockMovements.Add(new StockMovement { TenantId = scope.TenantId, BranchId = m.BranchId, DocumentId = stock.Id, ItemId = m.ItemId, WarehouseId = m.WarehouseId, Bucket = m.Bucket, Quantity = -m.Quantity, Value = -m.Value, UnitCost = m.UnitCost, Serials = m.Serials });
        var loanService = new CylinderLoanAccess(db, scope, costing);
        foreach (var loan in loans)
        {
            var issue = await db.CylinderLoanEntries.SingleAsync(x => x.LoanId == loan.Id && x.Kind == "Issue", ct);
            var e = await loanService.EntryAsync(loan, "IssueReversal", "internal:" + reversal.Id.ToString("N") + ":" + loan.Id.ToString("N"), hash, r.Reason, ct);
            e.Cash = -loan.Deposit; e.Liability = -loan.Deposit; e.ReversesId = issue.Id;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await SaleAsync(id, ct);
    }
}

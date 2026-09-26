using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed class PurchasingAccess(Gas365DbContext db, TenantScope scope, IInventoryCosting costing) : IPurchasingAccess
{
    private void Require(string permission)
    {
        if (!scope.Has(Permissions.PurchaseView) || !scope.Has(permission)) throw new AccessException(403, "You do not have permission for this purchasing action.");
    }
    private IQueryable<Branch> Branches() => db.Branches.Where(b => scope.IsOwner || scope.Has(Permissions.BranchManage) || db.BranchAccess.Any(a => a.MembershipId == scope.MembershipId && a.BranchId == b.Id));
    private async Task BranchAsync(Guid id, CancellationToken ct, bool active = false)
    { if (!await Branches().AnyAsync(x => x.Id == id && (!active || x.Active), ct)) throw new AccessException(404, "Branch not found."); }
    private async Task LockAsync(CancellationToken ct) => await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Tenants\" WHERE \"TenantId\" = {scope.TenantId} FOR UPDATE", ct);
    private void Audit(string action, Guid id) => db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId, ActorId = scope.UserId, Action = action, SubjectId = id });
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json)!;
    private static void Page(int page) { if (page is < 1 or > 1000000) throw new AccessException(400, "Invalid page."); }
    private static SupplierDto Dto(Supplier s) => new(s.Id, s.Name, s.Phone, s.Address, s.Active, s.Version);
    private static PurchaseEntryDto Dto(PurchaseEntry e) => new(e.Id, e.Kind, e.Payable, e.Cash, e.Reason, e.ActorId, e.PostedAt, e.BusinessDate, e.ReversesId);
    private async Task<Supplier> SupplierRow(Guid id, CancellationToken ct) => await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Supplier not found.");
    public async Task<PageDto<SupplierDto>> SuppliersAsync(string? search, int page, CancellationToken ct)
    {
        Require(Permissions.PurchaseView); Page(page);
        var query = db.Suppliers.AsNoTracking().Where(x => search == null || x.Name.Contains(search));
        var count = await query.CountAsync(ct);
        return new((await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20).ToArrayAsync(ct)).Select(Dto).ToArray(), count, page, 20);
    }
    public async Task<SupplierDto> SaveSupplierAsync(Guid? id, SupplierRequest r, CancellationToken ct)
    {
        Require(Permissions.SupplierManage);
        var s = id.HasValue ? await SupplierRow(id.Value, ct) : new Supplier { TenantId = scope.TenantId };
        if (id.HasValue && s.Version != r.Version) throw new AccessException(409, "Supplier changed. Refresh before saving.");
        s.Name = CatalogAccess.Text(r.Name); s.Phone = CatalogAccess.OptionalText(r.Phone, 80); s.Address = CatalogAccess.OptionalText(r.Address, 300); s.Active = r.Active;
        if (!id.HasValue) db.Suppliers.Add(s);
        Audit(id.HasValue ? "supplier.updated" : "supplier.created", s.Id); await db.SaveChangesAsync(ct); return Dto(s);
    }
    public async Task<SupplierProfileDto> SupplierAsync(Guid id, Guid branchId, int page, CancellationToken ct)
    {
        Require(Permissions.PurchaseView); Page(page); await BranchAsync(branchId, ct); var supplier = await SupplierRow(id, ct);
        var query = db.PurchaseEntries.AsNoTracking().Where(e => e.BranchId == branchId && e.Kind != "CostPayment" && e.Kind != "CostPaymentReversal" && db.PurchaseDocuments.Any(d => d.Id == e.PurchaseId && d.SupplierId == id));
        var balance = await query.SumAsync(x => x.Payable, ct); var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.PostedAt).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20).ToArrayAsync(ct);
        return new(Dto(supplier), balance, new(rows.Select(x => new SupplierLedgerDto(x.PurchaseId, Dto(x))).ToArray(), count, page, 20));
    }
    private async Task<PurchaseSnapshot> CalculateAsync(PurchaseDraftRequest r, CancellationToken ct)
    {
        await BranchAsync(r.BranchId, ct, true);
        if (!(await SupplierRow(r.SupplierId, ct)).Active) throw new AccessException(409, "Supplier is inactive.");
        if (r.Lines is null || r.Lines.Length is < 1 or > 100) throw new AccessException(400, "Provide 1–100 purchase lines.");
        if (r.CorrectsId.HasValue)
        {
            var corrected = await PurchaseAsync(r.CorrectsId.Value, ct);
            if (corrected.Status != "Reversed" || corrected.BranchId != r.BranchId) throw new AccessException(409, "A replacement must reference a reversed purchase in the same branch.");
        }
        PurchaseCostLine[] costs;
        try { costs = PurchaseCalculation.Allocate(r.Lines.Select(x => new PurchaseCostInput(x.Quantity, x.UnitCost, x.WeightKg)).ToArray(), r.LandedCharges, r.AllocationRule); }
        catch (ArgumentException e) { throw new AccessException(400, e.Message); }
        var lines = new List<PurchaseLineDto>();
        foreach (var (line, i) in r.Lines.Select((x, i) => (x, i)))
        {
            var item = await db.StockItems.SingleOrDefaultAsync(x => x.Id == line.ItemId && x.Active, ct) ?? throw new AccessException(404, "Active item not found.");
            var unit = await db.StockUnits.SingleAsync(x => x.Id == item.UnitId, ct);
            var warehouse = await db.Warehouses.SingleOrDefaultAsync(x => x.Id == line.WarehouseId && x.BranchId == r.BranchId && x.Active, ct) ?? throw new AccessException(404, "Active warehouse in this branch not found.");
            CatalogAccess.Decimal(line.Quantity, unit.DecimalPlaces, 1000000); CatalogAccess.Decimal(line.UnitCost, 6, 1000000); CatalogAccess.Decimal(line.WeightKg, 3, 1000000000);
            var bucket = InventoryAccess.Bucket(item, line.Bucket);
            var serials = (line.Serials ?? []).Select(s => CatalogAccess.Text(s, 80).ToUpperInvariant()).ToArray();
            if (serials.Length > 1000 || (item.SerialTracked ? serials.Length != line.Quantity || serials.Distinct().Count() != serials.Length : serials.Length != 0))
                throw new AccessException(400, "Provide one distinct registered serial per tracked cylinder, at most 1,000 per line.");
            var c = costs[i];
            lines.Add(new(item.Id, item.Sku, item.Name, unit.Code, warehouse.Id, warehouse.Name, bucket, line.Quantity, line.UnitCost, line.WeightKg, serials, c.BaseValue, c.LandedCost, c.Value, c.UnitCost));
        }
        var allSerials = lines.SelectMany(x => x.Serials).ToArray();
        if (allSerials.Distinct().Count() != allSerials.Length) throw new AccessException(400, "A serial cannot occur on multiple lines.");
        return new(CatalogAccess.Text(r.Reference, 120), CatalogAccess.OptionalText(r.Notes, 300), r.AllocationRule, r.LandedCharges, lines.ToArray());
    }
    public async Task<PurchaseDto> SaveDraftAsync(Guid? id, PurchaseDraftRequest r, CancellationToken ct)
    {
        Require(Permissions.PurchaseDraft);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        PurchaseDraft draft;
        if (id.HasValue)
        {
            draft = await db.PurchaseDrafts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Draft not found.");
            await BranchAsync(draft.BranchId, ct);
            if (await db.PurchaseDocuments.AnyAsync(x => x.DraftId == id, ct) || draft.Version != r.Version) throw new AccessException(409, "Draft changed or was posted. Refresh before saving.");
        }
        else { draft = new PurchaseDraft { TenantId = scope.TenantId }; db.PurchaseDrafts.Add(draft); }
        draft.Snapshot = JsonSerializer.Serialize(await CalculateAsync(r, ct));
        draft.BranchId = r.BranchId; draft.SupplierId = r.SupplierId; draft.Content = JsonSerializer.Serialize(r);
        Audit("purchase.draft_saved", draft.Id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await PurchaseAsync(draft.Id, ct);
    }
    public async Task<PageDto<PurchaseDto>> PurchasesAsync(Guid branchId, int page, CancellationToken ct)
    {
        Require(Permissions.PurchaseView); Page(page); await BranchAsync(branchId, ct);
        var q = db.PurchaseDrafts.Where(x => x.BranchId == branchId); var count = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(x => x.Id).Skip((page - 1) * 20).Take(20).Select(x => x.Id).ToArrayAsync(ct);
        var result = new List<PurchaseDto>(); foreach (var id in ids) result.Add(await PurchaseAsync(id, ct));
        return new(result.ToArray(), count, page, 20);
    }
    public async Task<PurchaseDto> PurchaseAsync(Guid id, CancellationToken ct)
    {
        Require(Permissions.PurchaseView);
        var doc = await db.PurchaseDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id || x.DraftId == id, ct);
        if (doc is null)
        {
            var d = await db.PurchaseDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Purchase not found.");
            await BranchAsync(d.BranchId, ct);
            var request = Parse<PurchaseDraftRequest>(d.Content);
            var snapshot = Parse<PurchaseSnapshot>(d.Snapshot);
            return new(d.Id, d.BranchId, d.SupplierId, (await SupplierRow(d.SupplierId, ct)).Name, "Draft", d.Version, snapshot.Lines.Sum(x => x.BaseValue), 0, snapshot.Lines.Sum(x => x.BaseValue), snapshot, null, request.CorrectsId, [], true, 0, snapshot.LandedCharges);
        }
        await BranchAsync(doc.BranchId, ct);
        var entries = await db.PurchaseEntries.AsNoTracking().Where(x => x.PurchaseId == doc.Id).OrderBy(x => x.PostedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var reversed = entries.Any(x => x.Kind == "Reversal"); var outstanding = entries.Sum(x => x.Payable);
        var paid = -entries.Where(x => x.Kind is "Payment" or "PaymentReversal").Sum(x => x.Cash);
        var costsPaid = -entries.Where(x => x.Kind is "CostPayment" or "CostPaymentReversal").Sum(x => x.Cash);
        var postedSnapshot = Parse<PurchaseSnapshot>(doc.Content);
        return new(doc.Id, doc.BranchId, doc.SupplierId, doc.SupplierName, reversed ? "Reversed" : outstanding == 0 ? "Paid" : paid > 0 ? "Partially Paid" : "Posted", 0, doc.Total, paid, outstanding, postedSnapshot, doc.StockDocumentId, doc.CorrectsId, entries.Select(Dto).ToArray(), doc.SeparateCosts, costsPaid, doc.SeparateCosts && !reversed ? postedSnapshot.LandedCharges - costsPaid : 0);
    }
    private async Task<PurchaseEntry?> ReplayAsync(string key, string hash, CancellationToken ct)
    {
        CatalogAccess.Text(key, 80);
        if (key.StartsWith("internal:", StringComparison.Ordinal)) throw new AccessException(400, "Reserved command key prefix.");
        var prior = await db.PurchaseEntries.SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (prior is not null && prior.RequestHash != hash) throw new AccessException(409, "Command key was used with different contents.");
        return prior;
    }
    private async Task<PurchaseEntry> EntryAsync(PurchaseDocument doc, string kind, decimal payable, decimal cash, string key, string hash, string reason, CancellationToken ct, Guid? reverses = null)
    {
        var now = DateTimeOffset.UtcNow; var tenant = await db.Tenants.SingleAsync(ct);
        var e = new PurchaseEntry
        {
            TenantId = scope.TenantId,
            BranchId = doc.BranchId,
            PurchaseId = doc.Id,
            ActorId = scope.UserId,
            Kind = kind,
            Payable = payable,
            Cash = cash,
            IdempotencyKey = key,
            RequestHash = hash,
            Reason = CatalogAccess.Text(reason, 300),
            PostedAt = now,
            BusinessDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone)).DateTime),
            ReversesId = reverses
        };
        db.PurchaseEntries.Add(e); Audit("purchase." + kind.ToLowerInvariant(), e.Id); return e;
    }
    public async Task<PurchaseDto> PostAsync(Guid id, PurchasePostRequest r, CancellationToken ct)
    {
        Require(Permissions.PurchasePost); if (r.CashPaid != 0) Require(Permissions.PurchasePay);
        var hash = Hash(new { Action = "post", id, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var replay = await ReplayAsync(r.IdempotencyKey, hash, ct); if (replay is not null) return await PurchaseAsync(replay.PurchaseId, ct);
        var draft = await db.PurchaseDrafts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Draft not found.");
        await BranchAsync(draft.BranchId, ct, true);
        if (draft.Version != r.Version || await db.PurchaseDocuments.AnyAsync(x => x.DraftId == id, ct)) throw new AccessException(409, "Draft changed or was posted.");
        var request = Parse<PurchaseDraftRequest>(draft.Content); var snapshot = await CalculateAsync(request, ct); var total = snapshot.Lines.Sum(x => x.BaseValue);
        CatalogAccess.Decimal(r.CashPaid, 2, total);
        var doc = new PurchaseDocument { TenantId = scope.TenantId, BranchId = draft.BranchId, SupplierId = draft.SupplierId, DraftId = id, SupplierName = (await SupplierRow(draft.SupplierId, ct)).Name, Content = JsonSerializer.Serialize(snapshot), Total = total, CorrectsId = request.CorrectsId, SeparateCosts = true };
        var invoice = await EntryAsync(doc, "Invoice", total, 0, r.IdempotencyKey, hash, snapshot.Reference, ct);
        var stock = new StockDocument { TenantId = scope.TenantId, BranchId = doc.BranchId, ActorId = scope.UserId, Kind = "PurchaseReceipt", Reason = snapshot.Reference, IdempotencyKey = "purchase:" + doc.Id, RequestHash = hash, PostedAt = invoice.PostedAt, BusinessDate = invoice.BusinessDate };
        db.StockDocuments.Add(stock); doc.StockDocumentId = stock.Id; db.PurchaseDocuments.Add(doc);
        var inventory = new InventoryAccess(db, scope, costing);
        foreach (var line in snapshot.Lines)
        {
            var item = await db.StockItems.SingleAsync(x => x.Id == line.ItemId, ct);
            await inventory.ValidateSerialsAsync(item, line.Serials, line.Quantity, line.WarehouseId, line.Bucket, false, ct);
            db.StockMovements.Add(new StockMovement { TenantId = scope.TenantId, BranchId = doc.BranchId, DocumentId = stock.Id, ItemId = line.ItemId, WarehouseId = line.WarehouseId, Bucket = line.Bucket, Quantity = line.Quantity, Value = line.Value, UnitCost = line.ReceiptUnitCost, Serials = line.Serials });
        }
        if (r.CashPaid > 0) await EntryAsync(doc, "Payment", -r.CashPaid, -r.CashPaid, "internal:" + doc.Id, hash, "Cash paid on posting", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await PurchaseAsync(doc.Id, ct);
    }
    private async Task<PurchaseDocument> PostedAsync(Guid id, CancellationToken ct)
    {
        var d = await db.PurchaseDocuments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Posted purchase not found.");
        await BranchAsync(d.BranchId, ct, true);
        if (await db.PurchaseEntries.AnyAsync(x => x.PurchaseId == id && x.Kind == "Reversal", ct)) throw new AccessException(409, "Purchase was reversed.");
        return d;
    }
    public async Task<PurchaseDto> PayAsync(Guid id, PurchasePaymentRequest r, CancellationToken ct)
    {
        Require(Permissions.PurchasePay); var hash = Hash(new { Action = "pay", id, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var replay = await ReplayAsync(r.IdempotencyKey, hash, ct); if (replay is not null) return await PurchaseAsync(replay.PurchaseId, ct);
        var doc = await PostedAsync(id, ct); var entries = await db.PurchaseEntries.Where(x => x.PurchaseId == id).OrderBy(x => x.PostedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        if (entries[^1].Id != r.ExpectedLastEntryId) throw new AccessException(409, "Payments changed. Refresh before paying.");
        CatalogAccess.Decimal(r.Amount, 2, entries.Sum(x => x.Payable));
        if (r.Amount <= 0) throw new AccessException(400, "Payment must be positive and cannot exceed outstanding payable.");
        await EntryAsync(doc, "Payment", -r.Amount, -r.Amount, r.IdempotencyKey, hash, r.Reason, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await PurchaseAsync(id, ct);
    }
    public async Task<PurchaseDto> PayCostsAsync(Guid id, PurchasePaymentRequest r, CancellationToken ct)
    {
        Require(Permissions.PurchasePay); var hash = Hash(new { Action = "payCosts", id, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var replay = await ReplayAsync(r.IdempotencyKey, hash, ct); if (replay is not null) return await PurchaseAsync(replay.PurchaseId, ct);
        var doc = await PostedAsync(id, ct);
        if (!doc.SeparateCosts) throw new AccessException(409, "Historical purchases retain their original supplier-paid cost treatment.");
        var entries = await db.PurchaseEntries.Where(x => x.PurchaseId == id).OrderBy(x => x.PostedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        if (entries[^1].Id != r.ExpectedLastEntryId) throw new AccessException(409, "Payments changed. Refresh before paying.");
        var outstanding = Parse<PurchaseSnapshot>(doc.Content).LandedCharges + entries.Where(x => x.Kind is "CostPayment" or "CostPaymentReversal").Sum(x => x.Cash);
        CatalogAccess.Decimal(r.Amount, 2, outstanding);
        if (r.Amount <= 0) throw new AccessException(400, "Payment must be positive and no greater than unpaid purchase costs.");
        await EntryAsync(doc, "CostPayment", 0, -r.Amount, r.IdempotencyKey, hash, r.Reason, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await PurchaseAsync(id, ct);
    }
    public async Task<PurchaseDto> ReversePaymentAsync(Guid id, Guid entryId, PurchaseReverseRequest r, CancellationToken ct)
    {
        Require(Permissions.PurchaseReverse); Require(Permissions.PurchasePay); var hash = Hash(new { Action = "reversePayment", id, entryId, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var replay = await ReplayAsync(r.IdempotencyKey, hash, ct); if (replay is not null) return await PurchaseAsync(replay.PurchaseId, ct);
        var doc = await PostedAsync(id, ct);
        var entry = await db.PurchaseEntries.SingleOrDefaultAsync(x => x.Id == entryId && x.PurchaseId == id && (x.Kind == "Payment" || x.Kind == "CostPayment"), ct) ?? throw new AccessException(404, "Payment not found.");
        if (await db.PurchaseEntries.AnyAsync(x => x.ReversesId == entryId, ct)) throw new AccessException(409, "Payment already reversed.");
        await EntryAsync(doc, entry.Kind == "CostPayment" ? "CostPaymentReversal" : "PaymentReversal", -entry.Payable, -entry.Cash, r.IdempotencyKey, hash, r.Reason, ct, entry.Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await PurchaseAsync(id, ct);
    }
    public async Task<PurchaseDto> ReverseAsync(Guid id, PurchaseReverseRequest r, CancellationToken ct)
    {
        Require(Permissions.PurchaseReverse); var hash = Hash(new { Action = "reverse", id, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var replay = await ReplayAsync(r.IdempotencyKey, hash, ct); if (replay is not null) return await PurchaseAsync(replay.PurchaseId, ct);
        var doc = await PostedAsync(id, ct);
        if (await db.PurchaseEntries.Where(x => x.PurchaseId == id).SumAsync(x => x.Cash, ct) != 0) throw new AccessException(409, "Reverse recorded payments before reversing the purchase.");
        var rows = await db.StockMovements.Where(x => x.DocumentId == doc.StockDocumentId).ToArrayAsync(ct);
        foreach (var pool in rows.GroupBy(x => new { x.ItemId, x.WarehouseId }))
        {
            var last = pool.Max(x => x.Sequence);
            if (await db.StockMovements.AnyAsync(x => x.ItemId == pool.Key.ItemId && x.WarehouseId == pool.Key.WarehouseId && x.Sequence > last, ct)) throw new AccessException(409, "Later stock movements prevent full purchase reversal.");
        }
        var serials = rows.SelectMany(x => x.Serials).ToArray(); var lastSequence = rows.Max(x => x.Sequence);
        if (serials.Length > 0 && await db.StockMovements.AnyAsync(x => x.Sequence > lastSequence && x.Serials.Any(s => serials.Contains(s)), ct)) throw new AccessException(409, "Later serial movements prevent reversal.");
        var invoice = await db.PurchaseEntries.SingleAsync(x => x.PurchaseId == id && x.Kind == "Invoice", ct);
        var reversal = await EntryAsync(doc, "Reversal", -doc.Total, 0, r.IdempotencyKey, hash, r.Reason, ct, invoice.Id);
        var stock = new StockDocument { TenantId = scope.TenantId, BranchId = doc.BranchId, ActorId = scope.UserId, Kind = "Reversal", Reason = r.Reason, IdempotencyKey = "purchase-reversal:" + reversal.Id, RequestHash = hash, ReversesId = doc.StockDocumentId, PostedAt = reversal.PostedAt, BusinessDate = reversal.BusinessDate };
        db.StockDocuments.Add(stock);
        foreach (var m in rows) db.StockMovements.Add(new StockMovement { TenantId = scope.TenantId, BranchId = m.BranchId, DocumentId = stock.Id, ItemId = m.ItemId, WarehouseId = m.WarehouseId, Bucket = m.Bucket, Quantity = -m.Quantity, Value = -m.Value, UnitCost = m.UnitCost, Serials = m.Serials });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await PurchaseAsync(id, ct);
    }
    public async Task<byte[]> VoucherAsync(Guid id, CancellationToken ct)
    {
        var purchase = await PurchaseAsync(id, ct);
        if (purchase.Status == "Draft") throw new AccessException(409, "Post the purchase before creating its voucher.");
        var bytes = PurchaseVoucher.Render(purchase);
        Audit("purchase.voucher_exported", purchase.Id); await db.SaveChangesAsync(ct); return bytes;
    }
}

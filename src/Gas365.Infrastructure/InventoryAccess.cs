using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed class InventoryAccess(Gas365DbContext db, TenantScope scope, IInventoryCosting costing) : IInventoryAccess
{
    private void Require(string permission)
    { if (!scope.Has(permission)) throw new AccessException(403, "You do not have permission for this action."); }
    private IQueryable<Warehouse> VisibleWarehouses() => db.Warehouses.Where(w => scope.IsOwner || scope.Has(Permissions.BranchManage) ||
        db.BranchAccess.Any(a => a.MembershipId == scope.MembershipId && a.BranchId == w.BranchId));
    private IQueryable<StockMovement> VisibleMovements() => db.StockMovements.Where(m => VisibleWarehouses().Any(w => w.Id == m.WarehouseId));
    private void Audit(string action, Guid id) => db.AuditEvents.Add(new AuditEvent
    { TenantId = scope.TenantId, ActorId = scope.UserId, Action = action, SubjectId = id });
    // All stock commands lock the tenant before reading balances. This also serializes retries,
    // absent stock buckets and cross-warehouse transfers without a deadlock-prone lock order.
    private async Task LockAsync(CancellationToken ct) => await db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT 1 FROM \"Tenants\" WHERE \"TenantId\" = {scope.TenantId} FOR UPDATE", ct);
    private async Task<Warehouse> WarehouseAsync(Guid id, CancellationToken ct) =>
        await VisibleWarehouses().SingleOrDefaultAsync(x => x.Id == id && x.Active && db.Branches.Any(b => b.Id == x.BranchId && b.Active), ct)
        ?? throw new AccessException(404, "Active warehouse not found.");
    private async Task<StockItem> ItemAsync(Guid id, CancellationToken ct) =>
        await db.StockItems.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Item not found.");
    private static MovementDto Dto(StockMovement m) => new(m.Id, m.Sequence, m.DocumentId, m.BranchId, m.WarehouseId, m.ItemId, m.Bucket, m.Quantity, m.Value, m.UnitCost, m.Serials);
    private static AssetDto Dto(CylinderAsset x) => new(x.Id, x.ItemId, x.Serial, x.Owner, x.InspectionReference, x.InspectionDue, x.Version);
    private static string Hash(object request) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
    private static string Key(string key) => CatalogAccess.Text(key, 80);
    internal static string Bucket(StockItem item, string bucket)
    {
        var allowed = item.Kind == "Cylinder" ? new[] { "Full", "Empty", "InTransit", "CustomerHeld", "Inspection", "Damaged", "Lost", "Retired" } : ["Available", "Damaged"];
        if (!allowed.Contains(bucket)) throw new AccessException(400, "Choose a supported stock state for this item.");
        return bucket;
    }
    private async Task<StockDocument?> ReplayAsync(string key, string hash, CancellationToken ct)
    {
        var prior = await db.StockDocuments.SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (prior is not null && prior.RequestHash != hash) throw new AccessException(409, "This command key was already used with different contents.");
        return prior;
    }
    public async Task<DocumentDto> DocumentAsync(Guid id, CancellationToken ct)
    {
        Require(Permissions.InventoryView);
        var doc = await db.StockDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Stock document not found.");
        var movements = await db.StockMovements.AsNoTracking().Where(x => x.DocumentId == id).OrderBy(x => x.Sequence).ToArrayAsync(ct);
        var visible = await VisibleWarehouses().Select(x => x.Id).ToArrayAsync(ct);
        if (movements.Any(x => !visible.Contains(x.WarehouseId))) throw new AccessException(404, "Stock document not found.");
        var reversedBy = await db.StockDocuments.Where(x => x.ReversesId == id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return new(doc.Id, doc.BranchId, doc.ActorId, doc.Kind, doc.Reason, doc.PostedAt, doc.BusinessDate, doc.ReversesId, reversedBy, movements.Select(Dto).ToArray());
    }
    public async Task<StockBalanceDto[]> BalancesAsync(Guid itemId, CancellationToken ct)
    {
        Require(Permissions.InventoryView); await ItemAsync(itemId, ct);
        var rows = await VisibleMovements().Where(x => x.ItemId == itemId)
            .GroupBy(x => new { x.WarehouseId, x.BranchId, x.Bucket })
            .Select(g => new { g.Key.WarehouseId, g.Key.BranchId, g.Key.Bucket, Quantity = g.Sum(x => x.Quantity), Value = g.Sum(x => x.Value), Sequence = g.Max(x => x.Sequence) }).ToArrayAsync(ct);
        return rows.GroupBy(x => new { x.WarehouseId, x.BranchId }).SelectMany(pool =>
        {
            var quantity = pool.Sum(x => x.Quantity); var value = pool.Sum(x => x.Value); var sequence = pool.Max(x => x.Sequence);
            return pool.GroupBy(x => x.Bucket).Select(bucket => new StockBalanceDto(itemId, pool.Key.WarehouseId, pool.Key.BranchId,
                bucket.Key, bucket.Sum(x => x.Quantity), value, costing.Average(quantity, value), sequence));
        }).OrderBy(x => x.WarehouseId).ThenBy(x => x.Bucket).ToArray();
    }
    public async Task<PageDto<MovementDto>> LedgerAsync(Guid itemId, Guid? warehouseId, int page, int pageSize, CancellationToken ct)
    {
        Require(Permissions.InventoryView); await ItemAsync(itemId, ct);
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100) throw new AccessException(400, "Invalid page range.");
        if (warehouseId.HasValue && !await VisibleWarehouses().AnyAsync(x => x.Id == warehouseId, ct)) throw new AccessException(404, "Warehouse not found.");
        var query = VisibleMovements().AsNoTracking().Where(x => x.ItemId == itemId && (!warehouseId.HasValue || x.WarehouseId == warehouseId));
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Sequence).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(Dto).ToArray(), count, page, pageSize);
    }
    public async Task<AssetDto[]> AssetsAsync(Guid itemId, CancellationToken ct)
    {
        Require(Permissions.CatalogManage); await ItemAsync(itemId, ct);
        return (await db.CylinderAssets.AsNoTracking().Where(x => x.ItemId == itemId).OrderBy(x => x.Serial).ToArrayAsync(ct)).Select(Dto).ToArray();
    }
    public async Task<AssetDto> SaveAssetAsync(Guid? id, AssetRequest request, CancellationToken ct)
    {
        Require(Permissions.CatalogManage);
        var item = await ItemAsync(request.ItemId, ct);
        if (!item.SerialTracked) throw new AccessException(400, "Choose a serial-tracked cylinder item.");
        var asset = id.HasValue ? await db.CylinderAssets.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Cylinder asset not found.") : new CylinderAsset { TenantId = scope.TenantId, ItemId = item.Id, Serial = CatalogAccess.Text(request.Serial, 80).ToUpperInvariant() };
        if (id.HasValue && asset.Version != request.Version) throw new AccessException(409, "This asset changed. Refresh and try again.");
        if (asset.ItemId != item.Id || asset.Serial != CatalogAccess.Text(request.Serial, 80).ToUpperInvariant()) throw new AccessException(409, "Serial and item are immutable.");
        asset.Owner = CatalogAccess.Text(request.Owner); asset.InspectionReference = CatalogAccess.OptionalText(request.InspectionReference); asset.InspectionDue = request.InspectionDue;
        if (!id.HasValue) db.CylinderAssets.Add(asset);
        Audit(id.HasValue ? "asset.updated" : "asset.created", asset.Id); await db.SaveChangesAsync(ct); return Dto(asset);
    }
    internal async Task ValidateSerialsAsync(StockItem item, string[] serials, decimal quantity, Guid warehouseId, string bucket, bool outgoing, CancellationToken ct)
    {
        if (!item.SerialTracked) { if (serials.Length != 0) throw new AccessException(400, "This item does not track serials."); return; }
        if (serials.Length != quantity || serials.Distinct().Count() != serials.Length) throw new AccessException(400, "Provide one distinct serial per cylinder.");
        if (await db.CylinderAssets.CountAsync(x => x.ItemId == item.Id && serials.Contains(x.Serial), ct) != serials.Length) throw new AccessException(404, "A cylinder serial is not registered for this item.");
        var history = await db.StockMovements.Where(x => x.ItemId == item.Id && x.Serials.Any(s => serials.Contains(s))).ToArrayAsync(ct);
        foreach (var serial in serials)
        {
            var locations = history.Where(x => x.Serials.Contains(serial)).GroupBy(x => new { x.WarehouseId, x.Bucket })
                .Select(g => new { g.Key, Count = g.Sum(x => x.Quantity > 0 ? 1 : -1) }).Where(x => x.Count != 0).ToArray();
            if (outgoing ? locations.Length != 1 || locations[0].Count != 1 || locations[0].Key.WarehouseId != warehouseId || locations[0].Key.Bucket != bucket : locations.Length != 0)
                throw new AccessException(409, "A cylinder serial is not available at the required location and state.");
        }
    }
    private StockMovement Movement(StockDocument doc, StockItem item, Warehouse warehouse, string bucket, decimal quantity, decimal value, decimal unitCost, string[] serials)
    {
        var row = new StockMovement
        {
            TenantId = scope.TenantId,
            DocumentId = doc.Id,
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            BranchId = warehouse.BranchId,
            Bucket = bucket,
            Quantity = quantity,
            Value = value,
            UnitCost = unitCost,
            Serials = serials
        };
        db.StockMovements.Add(row); return row;
    }
    private async Task<StockDocument> NewDocumentAsync(string kind, string key, string hash, string reason, Guid branchId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow; var tenant = await db.Tenants.SingleAsync(ct);
        var doc = new StockDocument
        {
            TenantId = scope.TenantId,
            BranchId = branchId,
            ActorId = scope.UserId,
            Kind = kind,
            IdempotencyKey = key,
            RequestHash = hash,
            Reason = CatalogAccess.Text(reason, 300),
            PostedAt = now,
            BusinessDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone)).DateTime)
        };
        db.StockDocuments.Add(doc); return doc;
    }
    public async Task<DocumentDto> PostAsync(PostStockRequest request, CancellationToken ct)
    {
        Require(Permissions.InventoryView); Require(Permissions.InventoryPost);
        var key = Key(request.IdempotencyKey); var hash = Hash(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var replay = await ReplayAsync(key, hash, ct); if (replay is not null) return await DocumentAsync(replay.Id, ct);
        if (request.Kind is not ("Opening" or "AdjustmentIn" or "AdjustmentOut" or "Transfer")) throw new AccessException(400, "Unsupported stock command.");
        var item = await ItemAsync(request.ItemId, ct); if (!item.Active) throw new AccessException(409, "This item is inactive.");
        var unit = await db.StockUnits.SingleAsync(x => x.Id == item.UnitId, ct);
        CatalogAccess.Decimal(request.Quantity, unit.DecimalPlaces);
        if (request.Quantity <= 0 || request.Quantity > 1000000) throw new AccessException(400, "Quantity must be positive and at most 1,000,000.");
        var serials = (request.Serials ?? []).Select(s => CatalogAccess.Text(s, 80).ToUpperInvariant()).ToArray();
        if (serials.Length > 1000) throw new AccessException(400, "Post at most 1,000 serials per command.");
        var source = await WarehouseAsync(request.WarehouseId, ct); var bucket = Bucket(item, request.Bucket);
        var rows = await db.StockMovements.Where(x => x.ItemId == item.Id && x.WarehouseId == source.Id)
            .GroupBy(x => x.Bucket).Select(g => new { Bucket = g.Key, Quantity = g.Sum(x => x.Quantity), Value = g.Sum(x => x.Value), Sequence = g.Max(x => x.Sequence) }).ToArrayAsync(ct);
        var sequence = rows.Select(x => x.Sequence).DefaultIfEmpty().Max();
        if (sequence != request.ExpectedSequence) throw new AccessException(409, "Stock changed. Refresh balances before posting.");
        var quantity = rows.Sum(x => x.Quantity); var value = rows.Sum(x => x.Value);
        var outgoing = request.Kind is "AdjustmentOut" or "Transfer";
        if (outgoing && rows.Where(x => x.Bucket == bucket).Sum(x => x.Quantity) < request.Quantity) throw new AccessException(409, "Insufficient stock in this state.");
        await ValidateSerialsAsync(item, serials, request.Quantity, source.Id, bucket, outgoing, ct);
        if (request.Kind == "Opening" && rows.Any(x => x.Bucket == bucket)) throw new AccessException(409, "Opening stock already exists for this state; use an adjustment.");
        decimal cost; decimal amount;
        if (outgoing)
        {
            if (request.UnitCost is not null) throw new AccessException(400, "Issue and transfer costs are calculated by the server.");
            cost = costing.Average(quantity, value); amount = costing.IssueValue(quantity, value, request.Quantity);
        }
        else
        {
            if (request.UnitCost is null) throw new AccessException(400, "Receipt unit cost is required.");
            CatalogAccess.Decimal(request.UnitCost.Value, 6, 1000000m); cost = request.UnitCost.Value; amount = costing.ReceiptValue(request.Quantity, cost);
        }
        Warehouse? destination = null; string? destinationBucket = null;
        if (request.Kind == "Transfer")
        {
            destination = await WarehouseAsync(request.DestinationWarehouseId ?? Guid.Empty, ct); destinationBucket = Bucket(item, request.DestinationBucket ?? "");
            if (destination.Id == source.Id && destinationBucket == bucket) throw new AccessException(400, "Choose a different location or state.");
            var destinationSequence = await db.StockMovements.Where(x => x.ItemId == item.Id && x.WarehouseId == destination.Id).Select(x => (long?)x.Sequence).MaxAsync(ct) ?? 0;
            if (destinationSequence != request.DestinationExpectedSequence) throw new AccessException(409, "Destination stock changed. Refresh before posting.");
        }
        else if (request.DestinationWarehouseId.HasValue || request.DestinationBucket is not null) throw new AccessException(400, "Only transfers have a destination.");
        var doc = await NewDocumentAsync(request.Kind, key, hash, request.Reason, source.BranchId, ct);
        Movement(doc, item, source, bucket, outgoing ? -request.Quantity : request.Quantity, outgoing ? -amount : amount, cost, serials);
        if (destination is not null) Movement(doc, item, destination, destinationBucket!, request.Quantity, amount, cost, serials);
        Audit("stock.posted", doc.Id); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return await DocumentAsync(doc.Id, ct);
    }
    public async Task<DocumentDto> ReverseAsync(Guid id, ReverseStockRequest request, CancellationToken ct)
    {
        Require(Permissions.InventoryView); Require(Permissions.InventoryReverse);
        var key = Key(request.IdempotencyKey); var hash = Hash(new { Id = id, Request = request });
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var replay = await ReplayAsync(key, hash, ct); if (replay is not null) return await DocumentAsync(replay.Id, ct);
        var original = await DocumentAsync(id, ct);
        if (original.Kind == "PurchaseReceipt") throw new AccessException(409, "Reverse this receipt through its purchase to preserve the supplier ledger.");
        if (original.ReversesId.HasValue || original.ReversedById.HasValue) throw new AccessException(409, "This document cannot be reversed again.");
        foreach (var pool in original.Movements.GroupBy(x => new { x.ItemId, x.WarehouseId }))
        {
            var last = pool.Max(x => x.Sequence);
            if (await db.StockMovements.AnyAsync(x => x.ItemId == pool.Key.ItemId && x.WarehouseId == pool.Key.WarehouseId && x.Sequence > last, ct))
                throw new AccessException(409, "Later stock movements exist. A reviewed adjustment is required instead.");
        }
        // A serial can be reintroduced elsewhere after an issue; also enforce serial-wide dependencies.
        var serials = original.Movements.SelectMany(x => x.Serials).Distinct().ToArray();
        var originalLast = original.Movements.Max(x => x.Sequence);
        if (serials.Length > 0 && await db.StockMovements.AnyAsync(x => x.Sequence > originalLast && x.Serials.Any(s => serials.Contains(s)), ct))
            throw new AccessException(409, "Later cylinder movements depend on this document.");
        var doc = await NewDocumentAsync("Reversal", key, hash, request.Reason, original.BranchId, ct); doc.ReversesId = id;
        foreach (var movement in original.Movements)
        {
            db.StockMovements.Add(new StockMovement
            {
                TenantId = scope.TenantId,
                DocumentId = doc.Id,
                BranchId = movement.BranchId,
                ItemId = movement.ItemId,
                WarehouseId = movement.WarehouseId,
                Bucket = movement.Bucket,
                Quantity = -movement.Quantity,
                Value = -movement.Value,
                UnitCost = movement.UnitCost,
                Serials = movement.Serials
            });
        }
        Audit("stock.reversed", doc.Id); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return await DocumentAsync(doc.Id, ct);
    }
}

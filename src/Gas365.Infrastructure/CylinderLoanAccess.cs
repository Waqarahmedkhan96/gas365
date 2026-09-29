using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed class CylinderLoanAccess(Gas365DbContext db, TenantScope scope, IInventoryCosting costing) : ICylinderLoanAccess
{
    private void Require(string permission)
    { if (!scope.Has(Permissions.SalesView) || !scope.Has(permission)) throw new AccessException(403, "You do not have permission for this cylinder action."); }
    private async Task BranchAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == id && (scope.IsOwner || scope.Has(Permissions.BranchManage) || db.BranchAccess.Any(a => a.MembershipId == scope.MembershipId && a.BranchId == b.Id)), ct)) throw new AccessException(404, "Branch not found.");
    }
    private async Task LockAsync(CancellationToken ct) => await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Tenants\" WHERE \"TenantId\" = {scope.TenantId} FOR UPDATE", ct);
    private static string Hash(object r) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r))));
    private void Audit(string action, Guid id) => db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId, ActorId = scope.UserId, Action = action, SubjectId = id });
    private static CylinderPolicyDto Dto(CylinderPolicy x) => new(x.ItemId, x.GasItemId, x.GasQuantity, x.SuggestedDeposit, x.UsageTerms, x.Version);
    public async Task<CylinderPolicyDto?> PolicyAsync(Guid itemId, CancellationToken ct)
    {
        Require(Permissions.SalesView);
        if (!await db.StockItems.AnyAsync(x => x.Id == itemId && x.Kind == "Cylinder", ct)) throw new AccessException(404, "Cylinder type not found.");
        var row = await db.CylinderPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == itemId, ct); return row is null ? null : Dto(row);
    }
    public async Task<CylinderPolicyDto> SavePolicyAsync(Guid itemId, CylinderPolicyRequest r, CancellationToken ct)
    {
        Require(Permissions.CylinderManage);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        if (!await db.StockItems.AnyAsync(x => x.Id == itemId && x.Kind == "Cylinder" && x.Active, ct)) throw new AccessException(404, "Active cylinder type not found.");
        var gas = await db.StockItems.SingleOrDefaultAsync(x => x.Id == r.GasItemId && x.Kind == "Lpg" && x.Active, ct) ?? throw new AccessException(404, "Active LPG item not found.");
        var unit = await db.StockUnits.SingleAsync(x => x.Id == gas.UnitId, ct);
        if (!string.Equals(unit.Code, "KG", StringComparison.OrdinalIgnoreCase)) throw new AccessException(400, "Cylinder LPG must use a KG catalog unit.");
        CatalogAccess.Decimal(r.GasQuantity, unit.DecimalPlaces, 1000000); CatalogAccess.Decimal(r.SuggestedDeposit, 2, 1000000000);
        if (r.GasQuantity <= 0) throw new AccessException(400, "Filled LPG kilograms must be positive.");
        var row = await db.CylinderPolicies.SingleOrDefaultAsync(x => x.ItemId == itemId, ct);
        if (row is not null && row.Version != r.Version) throw new AccessException(409, "Cylinder policy changed.");
        if (row is null) { row = new CylinderPolicy { TenantId = scope.TenantId, ItemId = itemId }; db.CylinderPolicies.Add(row); }
        row.GasItemId = r.GasItemId; row.GasQuantity = r.GasQuantity; row.SuggestedDeposit = r.SuggestedDeposit; row.UsageTerms = CatalogAccess.Text(r.UsageTerms, 300);
        Audit("cylinder.policy_saved", row.Id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Dto(row);
    }
    public async Task<DepositArrangementDto> ArrangementAsync(Guid customerId, CancellationToken ct)
    {
        Require(Permissions.SalesView);
        if (!await db.Customers.AnyAsync(x => x.Id == customerId, ct)) throw new AccessException(404, "Customer not found.");
        var row = await db.CustomerDepositArrangements.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == customerId, ct);
        return new(customerId, row?.ZeroDepositApproved ?? false, row?.Reason ?? "", row?.Version ?? 0);
    }
    public async Task<DepositArrangementDto> SaveArrangementAsync(Guid customerId, DepositArrangementRequest r, CancellationToken ct)
    {
        Require(Permissions.CylinderWaive);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        await ArrangementAsync(customerId, ct);
        var row = await db.CustomerDepositArrangements.SingleOrDefaultAsync(x => x.CustomerId == customerId, ct);
        if (row is not null && row.Version != r.Version) throw new AccessException(409, "Deposit arrangement changed.");
        if (row is null) { row = new CustomerDepositArrangement { TenantId = scope.TenantId, CustomerId = customerId }; db.CustomerDepositArrangements.Add(row); }
        row.ZeroDepositApproved = r.ZeroDepositApproved; row.Reason = CatalogAccess.Text(r.Reason, 300); row.ApprovedBy = scope.UserId;
        Audit("cylinder.deposit_arrangement", row.Id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(customerId, row.ZeroDepositApproved, row.Reason, row.Version);
    }
    private async Task<CylinderLoan> RowAsync(Guid id, CancellationToken ct)
    {
        var loan = await db.CylinderLoans.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Cylinder loan not found.");
        await BranchAsync(loan.BranchId, ct); return loan;
    }
    private Task<CylinderLoanEntry[]> EntriesAsync(Guid id, CancellationToken ct) => db.CylinderLoanEntries.Where(x => x.LoanId == id).OrderBy(x => x.Sequence).ToArrayAsync(ct);
    private static bool Active(CylinderLoanEntry row, CylinderLoanEntry[] entries) => !entries.Any(x => x.ReversesId == row.Id);
    private static void Current(CylinderLoanEntry[] entries, Guid expected)
    { if (entries.Length == 0 || entries[^1].Id != expected) throw new AccessException(409, "Cylinder history changed. Refresh before continuing."); }
    private static void Open(CylinderLoanEntry[] entries)
    { if (entries.Any(x => x.Kind == "IssueReversal" || x.Kind == "Return" && Active(x, entries))) throw new AccessException(409, "Cylinder is no longer on loan."); }
    public async Task<CylinderLoanDto> LoanAsync(Guid id, CancellationToken ct)
    {
        Require(Permissions.SalesView); var loan = await RowAsync(id, ct); var entries = await EntriesAsync(id, ct);
        var returned = entries.LastOrDefault(x => x.Kind == "Return" && Active(x, entries)); var cancelled = entries.Any(x => x.Kind == "IssueReversal");
        return new(loan.Id, loan.SaleId, loan.BranchId, loan.CustomerId, loan.ItemId, loan.WarehouseId, loan.Serial, loan.Deposit, loan.UsageTerms,
            cancelled ? "Reversed" : returned is null ? "On loan" : returned.Condition == "Lost" ? "Lost" : "Returned",
            returned is null ? 0 : entries.Sum(x => x.Liability), entries.Sum(x => x.Receivable),
            entries.Select(e => new LoanEntryDto(e.Id, e.Kind, e.Cash, e.Liability, e.Receivable, e.Revenue, e.UsageFee, e.DamageOrLossCharge, e.Condition, e.Reason, e.ActorId, e.PostedAt, e.BusinessDate, e.ReversesId, e.ApprovalId)).ToArray());
    }
    public async Task<PageDto<CylinderLoanDto>> LoansAsync(Guid branchId, Guid? customerId, int page, CancellationToken ct)
    {
        Require(Permissions.SalesView); await BranchAsync(branchId, ct);
        if (page is < 1 or > 1000000) throw new AccessException(400, "Invalid page.");
        if (customerId.HasValue && !await db.Customers.AnyAsync(x => x.Id == customerId, ct)) throw new AccessException(404, "Customer not found.");
        var q = db.CylinderLoans.Where(x => x.BranchId == branchId && (!customerId.HasValue || x.CustomerId == customerId));
        var count = await q.CountAsync(ct); var ids = await q.OrderByDescending(x => x.Id).Skip((page - 1) * 20).Take(20).Select(x => x.Id).ToArrayAsync(ct);
        var rows = new List<CylinderLoanDto>(); foreach (var id in ids) rows.Add(await LoanAsync(id, ct)); return new(rows.ToArray(), count, page, 20);
    }
    private async Task<CylinderLoanEntry?> ReplayAsync(string key, string hash, CancellationToken ct)
    {
        if (CatalogAccess.Text(key, 80) != key || key.StartsWith("internal:", StringComparison.Ordinal)) throw new AccessException(400, "Invalid command key.");
        var prior = await db.CylinderLoanEntries.SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (prior is not null && prior.RequestHash != hash) throw new AccessException(409, "Command key has different contents."); return prior;
    }
    internal async Task<CylinderLoanEntry> EntryAsync(CylinderLoan loan, string kind, string key, string hash, string reason, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow; var tenant = await db.Tenants.SingleAsync(ct);
        var entry = new CylinderLoanEntry
        {
            TenantId = scope.TenantId,
            BranchId = loan.BranchId,
            LoanId = loan.Id,
            ActorId = scope.UserId,
            Kind = kind,
            IdempotencyKey = key,
            RequestHash = hash,
            Reason = CatalogAccess.Text(reason, 300),
            PostedAt = now,
            BusinessDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone)).DateTime)
        };
        db.CylinderLoanEntries.Add(entry); Audit("cylinder." + kind.ToLowerInvariant(), entry.Id); return entry;
    }
    public async Task<CylinderLoanDto> ApproveAsync(Guid id, LoanApprovalRequest r, CancellationToken ct)
    {
        Require(Permissions.CylinderApprove); var hash = Hash(new { Action = "approve", id, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var prior = await ReplayAsync(r.IdempotencyKey, hash, ct); if (prior is not null) return await LoanAsync(prior.LoanId, ct);
        var loan = await RowAsync(id, ct); var entries = await EntriesAsync(id, ct); Current(entries, r.ExpectedLastEntryId); Open(entries);
        if (r.Condition is not ("Damaged" or "Lost" or "Inspection")) throw new AccessException(400, "Choose damaged, lost or inspection for a damage/loss approval.");
        CatalogAccess.Decimal(r.Charge, 2, 1000000000);
        var e = await EntryAsync(loan, "Approval", r.IdempotencyKey, hash, r.Reason, ct); e.DamageOrLossCharge = r.Charge; e.Condition = r.Condition;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await LoanAsync(id, ct);
    }
    public async Task<CylinderLoanDto> ReturnAsync(Guid id, LoanReturnRequest r, CancellationToken ct)
    {
        Require(Permissions.CylinderReturn); var hash = Hash(new { Action = "return", id, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var prior = await ReplayAsync(r.IdempotencyKey, hash, ct); if (prior is not null) return await LoanAsync(prior.LoanId, ct);
        var loan = await RowAsync(id, ct); var entries = await EntriesAsync(id, ct); Current(entries, r.ExpectedLastEntryId); Open(entries);
        if (r.Condition is not ("Empty" or "Inspection" or "Damaged" or "Lost")) throw new AccessException(400, "Choose the cylinder return condition.");
        var approval = r.ApprovalId.HasValue ? entries.SingleOrDefault(x => x.Id == r.ApprovalId && x.Kind == "Approval" && Active(x, entries)) : null;
        if (r.ApprovalId.HasValue && (approval is null || approval.Condition != r.Condition)) throw new AccessException(409, "Approval does not match this loan and condition.");
        if (r.Condition is "Damaged" or "Lost" && approval is null) throw new AccessException(403, "Damage or loss requires manager approval.");
        CatalogAccess.Decimal(r.UsageFee, 2, 1000000000);
        var amounts = CylinderSettlement.Calculate(loan.Deposit, r.UsageFee, approval?.DamageOrLossCharge ?? 0);
        var e = await EntryAsync(loan, "Return", r.IdempotencyKey, hash, r.Reason, ct);
        e.Liability = -amounts.AppliedDeposit; e.Receivable = amounts.AmountToCollect; e.Revenue = amounts.Revenue;
        e.UsageFee = r.UsageFee; e.DamageOrLossCharge = amounts.DamageOrLossCharge; e.Condition = r.Condition; e.ApprovalId = approval?.Id;
        var stock = new StockDocument { TenantId = scope.TenantId, BranchId = loan.BranchId, ActorId = scope.UserId, Kind = "CylinderReturn", Reason = e.Reason, IdempotencyKey = "loan-return:" + e.Id, RequestHash = hash, PostedAt = e.PostedAt, BusinessDate = e.BusinessDate };
        db.StockDocuments.Add(stock); e.StockDocumentId = stock.Id;
        var item = await db.StockItems.SingleAsync(x => x.Id == loan.ItemId, ct); var serials = loan.Serial.Length == 0 ? Array.Empty<string>() : [loan.Serial];
        await new InventoryAccess(db, scope, costing).ValidateSerialsAsync(item, serials, 1, loan.WarehouseId, "CustomerHeld", true, ct);
        var pool = db.StockMovements.Where(x => x.ItemId == loan.ItemId && x.WarehouseId == loan.WarehouseId);
        if (await pool.Where(x => x.Bucket == "CustomerHeld").SumAsync(x => x.Quantity, ct) < 1) throw new AccessException(409, "Customer-held stock is inconsistent.");
        var quantity = await pool.SumAsync(x => x.Quantity, ct); var value = await pool.SumAsync(x => x.Value, ct); var amount = costing.IssueValue(quantity, value, 1); var cost = costing.Average(quantity, value);
        foreach (var (bucket, sign) in new[] { ("CustomerHeld", -1), (r.Condition, 1) })
            db.StockMovements.Add(new StockMovement { TenantId = scope.TenantId, BranchId = loan.BranchId, DocumentId = stock.Id, ItemId = loan.ItemId, WarehouseId = loan.WarehouseId, Bucket = bucket, Quantity = sign, Value = sign * amount, UnitCost = cost, Serials = serials });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await LoanAsync(id, ct);
    }
    public Task<CylinderLoanDto> RefundAsync(Guid id, LoanPaymentRequest r, CancellationToken ct) => MoneyAsync(id, r, true, ct);
    public Task<CylinderLoanDto> PayAsync(Guid id, LoanPaymentRequest r, CancellationToken ct) => MoneyAsync(id, r, false, ct);
    private async Task<CylinderLoanDto> MoneyAsync(Guid id, LoanPaymentRequest r, bool refund, CancellationToken ct)
    {
        Require(Permissions.SalesPay); var hash = Hash(new { Action = refund ? "refund" : "pay", id, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var prior = await ReplayAsync(r.IdempotencyKey, hash, ct); if (prior is not null) return await LoanAsync(prior.LoanId, ct);
        var loan = await RowAsync(id, ct); var entries = await EntriesAsync(id, ct); Current(entries, r.ExpectedLastEntryId);
        if (!entries.Any(x => x.Kind == "Return" && Active(x, entries))) throw new AccessException(409, "Return or settle the cylinder before processing money.");
        CatalogAccess.Decimal(r.Amount, 2, refund ? entries.Sum(x => x.Liability) : entries.Sum(x => x.Receivable));
        if (r.Amount <= 0) throw new AccessException(400, "Amount must be positive and no greater than the outstanding amount.");
        var e = await EntryAsync(loan, refund ? "Refund" : "Payment", r.IdempotencyKey, hash, r.Reason, ct);
        e.Cash = refund ? -r.Amount : r.Amount; e.Liability = refund ? -r.Amount : 0; e.Receivable = refund ? 0 : -r.Amount;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await LoanAsync(id, ct);
    }
    public async Task<CylinderLoanDto> ReverseAsync(Guid id, Guid entryId, SaleReverseRequest r, CancellationToken ct)
    {
        Require(Permissions.SalesReverse); var hash = Hash(new { Action = "reverse", id, entryId, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var prior = await ReplayAsync(r.IdempotencyKey, hash, ct); if (prior is not null) return await LoanAsync(prior.LoanId, ct);
        var loan = await RowAsync(id, ct); var entries = await EntriesAsync(id, ct);
        var original = entries.SingleOrDefault(x => x.Id == entryId && x.Kind is "Return" or "Refund" or "Payment") ?? throw new AccessException(404, "Reversible cylinder entry not found.");
        if (!Active(original, entries)) throw new AccessException(409, "Entry already reversed.");
        if (original.Cash != 0) Require(Permissions.SalesPay);
        if (original.Kind == "Return" && entries.Any(x => x.Sequence > original.Sequence && x.Kind is "Refund" or "Payment" or "Allocation" && Active(x, entries))) throw new AccessException(409, "Reverse later cash entries and advance allocations before reversing the return.");
        var e = await EntryAsync(loan, original.Kind + "Reversal", r.IdempotencyKey, hash, r.Reason, ct);
        e.Cash = -original.Cash; e.Liability = -original.Liability; e.Receivable = -original.Receivable; e.Revenue = -original.Revenue; e.UsageFee = -original.UsageFee; e.DamageOrLossCharge = -original.DamageOrLossCharge; e.ReversesId = original.Id;
        if (original.StockDocumentId.HasValue)
        {
            var rows = await db.StockMovements.Where(x => x.DocumentId == original.StockDocumentId).ToArrayAsync(ct); var last = rows.Max(x => x.Sequence);
            var serials = rows.SelectMany(x => x.Serials).ToArray();
            if (await db.StockMovements.AnyAsync(x => x.Sequence > last && (x.ItemId == loan.ItemId && x.WarehouseId == loan.WarehouseId || x.Serials.Any(s => serials.Contains(s))), ct)) throw new AccessException(409, "Later stock movements prevent return reversal.");
            var stock = new StockDocument { TenantId = scope.TenantId, BranchId = loan.BranchId, ActorId = scope.UserId, Kind = "CylinderReturnReversal", Reason = e.Reason, IdempotencyKey = "loan-reversal:" + e.Id, RequestHash = hash, PostedAt = e.PostedAt, BusinessDate = e.BusinessDate, ReversesId = original.StockDocumentId };
            db.StockDocuments.Add(stock); e.StockDocumentId = stock.Id;
            foreach (var m in rows) db.StockMovements.Add(new StockMovement { TenantId = scope.TenantId, BranchId = m.BranchId, DocumentId = stock.Id, ItemId = m.ItemId, WarehouseId = m.WarehouseId, Bucket = m.Bucket, Quantity = -m.Quantity, Value = -m.Value, UnitCost = m.UnitCost, Serials = m.Serials });
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await LoanAsync(id, ct);
    }
}

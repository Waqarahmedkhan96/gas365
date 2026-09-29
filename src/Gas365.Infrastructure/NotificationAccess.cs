using System.Globalization;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

internal static class BranchScope
{
    // Branches the member may see: all for owners and branch managers, otherwise their grants.
    internal static IQueryable<Branch> Accessible(Gas365DbContext db, TenantScope scope) =>
        scope.IsOwner || scope.Has(Permissions.BranchManage) ? db.Branches.AsNoTracking()
            : db.Branches.AsNoTracking().Where(b => db.BranchAccess.Any(a => a.MembershipId == scope.MembershipId && a.BranchId == b.Id));
}

// Members see active alerts whose permission they hold, for branches they can access.
public sealed class NotificationAccess(Gas365DbContext db, TenantScope scope) : INotificationAccess
{
    private async Task<IQueryable<Notification>> VisibleAsync(CancellationToken ct)
    {
        var branches = await BranchScope.Accessible(db, scope).Select(b => b.Id).ToArrayAsync(ct);
        var permissions = scope.Permissions;
        return db.Notifications.AsNoTracking().Where(n => n.ResolvedAt == null && permissions.Contains(n.Permission) && (n.BranchId == null || branches.Contains(n.BranchId.Value)));
    }

    public async Task<PageDto<NotificationDto>> PageAsync(int page, CancellationToken ct)
    {
        if (page is < 1 or > 100_000) throw new AccessException(400, "Invalid page.");
        var query = await VisibleAsync(ct);
        var rows = await query.OrderByDescending(n => n.CreatedAt).Skip((page - 1) * 20).Take(20)
            .Select(n => new NotificationDto(n.Id, n.Kind, n.Title, n.Body, n.BranchId, n.CreatedAt, db.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == scope.UserId)))
            .ToArrayAsync(ct);
        return new(rows, await query.CountAsync(ct), page, 20);
    }

    public async Task<NotificationSummaryDto> SummaryAsync(CancellationToken ct) =>
        new(await (await VisibleAsync(ct)).CountAsync(n => !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == scope.UserId), ct));

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        if (!await (await VisibleAsync(ct)).AnyAsync(n => n.Id == id, ct)) throw new AccessException(404, "Notification not found.");
        if (await db.NotificationReads.AnyAsync(r => r.NotificationId == id && r.UserId == scope.UserId, ct)) return;
        db.NotificationReads.Add(new NotificationRead { TenantId = scope.TenantId, NotificationId = id, UserId = scope.UserId });
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var unread = await (await VisibleAsync(ct)).Where(n => !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == scope.UserId)).Select(n => n.Id).ToArrayAsync(ct);
        db.NotificationReads.AddRange(unread.Select(id => new NotificationRead { TenantId = scope.TenantId, NotificationId = id, UserId = scope.UserId }));
        await db.SaveChangesAsync(ct);
    }
}

// Rebuilds the desired set of alerts per tenant and reconciles it with active rows: new conditions are inserted,
// changed text is refreshed, and cleared conditions are resolved. Idempotent; a tenant advisory lock lets several
// API instances run it without duplicating work.
public sealed class NotificationSweeper(Gas365DbContext db, TenantScope scope) : INotificationSweeper
{
    private sealed record Desired(string Kind, string Permission, Guid? BranchId, Guid SubjectId, string Title, string Body);

    public async Task<int> SweepAsync(CancellationToken ct)
    {
        var tenants = await db.Tenants.IgnoreQueryFilters().AsNoTracking().ToArrayAsync(ct);
        var changes = 0;
        foreach (var tenant in tenants)
        {
            scope.TenantId = tenant.TenantId; db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var key = "notifications:" + tenant.TenantId;
            if (!await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtext({key})) AS \"Value\"").SingleAsync(ct)) continue;
            var zone = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone);
            var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
            var desired = new Dictionary<string, Desired>();
            var branches = await db.Branches.AsNoTracking().Where(b => b.Active).ToDictionaryAsync(b => b.Id, b => b.Name, ct);
            await LowStockAsync(desired, branches, ct);
            await OverdueAsync(desired, branches, DateOnly.FromDateTime(local.DateTime), tenant.OverdueCreditDays, ct);
            await RegistersAsync(desired, branches, DateOnly.FromDateTime(local.DateTime), TimeOnly.FromDateTime(local.DateTime) >= tenant.ClosingReminder, ct);
            var active = await db.Notifications.Where(n => n.ResolvedAt == null).ToDictionaryAsync(n => n.Key, ct);
            var now = DateTimeOffset.UtcNow;
            foreach (var (k, d) in desired)
            {
                if (active.TryGetValue(k, out var existing))
                {
                    if (existing.Title != d.Title || existing.Body != d.Body) { existing.Title = d.Title; existing.Body = d.Body; changes++; }
                }
                else
                {
                    db.Notifications.Add(new Notification { TenantId = tenant.TenantId, Key = k, Kind = d.Kind, Permission = d.Permission, BranchId = d.BranchId, SubjectId = d.SubjectId, Title = d.Title, Body = d.Body });
                    changes++;
                }
            }
            foreach (var stale in active.Values.Where(n => !desired.ContainsKey(n.Key))) { stale.ResolvedAt = now; changes++; }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        return changes;
    }

    private static string Money(decimal value) => "PKR " + value.ToString("N2", CultureInfo.InvariantCulture);

    // Only branches that have held the item are checked, so a new catalog item does not alert everywhere.
    private async Task LowStockAsync(Dictionary<string, Desired> desired, Dictionary<Guid, string> branches, CancellationToken ct)
    {
        var levels = await (from m in db.StockMovements.AsNoTracking()
                            join i in db.StockItems.AsNoTracking() on m.ItemId equals i.Id
                            join u in db.StockUnits.AsNoTracking() on i.UnitId equals u.Id
                            where i.Active && i.ReorderLevel > 0
                            group m by new { i.Id, i.Name, i.ReorderLevel, u.Code, m.BranchId } into g
                            select new { g.Key.Id, g.Key.Name, g.Key.ReorderLevel, g.Key.Code, g.Key.BranchId, Quantity = g.Sum(x => x.Bucket == "Available" || x.Bucket == "Full" ? x.Quantity : 0m) }).ToArrayAsync(ct);
        foreach (var l in levels.Where(l => l.Quantity <= l.ReorderLevel && branches.ContainsKey(l.BranchId)))
            desired[$"low-stock:{l.Id}:{l.BranchId}"] = new(NotificationKinds.LowStock, Permissions.InventoryView, l.BranchId, l.Id,
                "Low stock: " + l.Name, $"{l.Quantity.ToString("0.###", CultureInfo.InvariantCulture)} {l.Code} sellable at {branches[l.BranchId]}; reorder level {l.ReorderLevel.ToString("0.###", CultureInfo.InvariantCulture)}.");
    }

    private async Task OverdueAsync(Dictionary<string, Desired> desired, Dictionary<Guid, string> branches, DateOnly today, int days, CancellationToken ct)
    {
        // Aggregated in SQL so only open balances leave the database.
        var items = new List<(Guid Customer, Guid Branch, DateOnly Date, decimal Outstanding)>();
        // Each journal is aggregated per document in a subquery; only open balances are joined and returned.
        var openSales = db.SaleEntries.AsNoTracking().GroupBy(e => e.SaleId)
            .Select(g => new { Id = g.Key, Outstanding = g.Sum(x => x.Receivable), Invoices = g.Count(x => x.Kind == "Invoice"), Date = g.Min(x => x.Kind == "Invoice" ? (DateOnly?)x.BusinessDate : null) })
            .Where(x => x.Outstanding > 0 && x.Invoices > 0);
        var sales = await (from o in openSales
                           join d in db.SaleDocuments.AsNoTracking() on o.Id equals d.Id
                           select new { d.CustomerId, d.BranchId, o.Date, o.Outstanding }).ToArrayAsync(ct);
        items.AddRange(sales.Select(x => (x.CustomerId, x.BranchId, x.Date!.Value, x.Outstanding)));
        var openLoans = db.CylinderLoanEntries.AsNoTracking().GroupBy(e => e.LoanId)
            .Select(g => new { Id = g.Key, Outstanding = g.Sum(x => x.Receivable), Date = g.Min(x => x.Receivable > 0 ? (DateOnly?)x.BusinessDate : null) })
            .Where(x => x.Outstanding > 0);
        var loans = await (from o in openLoans
                           join l in db.CylinderLoans.AsNoTracking() on o.Id equals l.Id
                           select new { l.CustomerId, l.BranchId, o.Date, o.Outstanding }).ToArrayAsync(ct);
        items.AddRange(loans.Select(x => (x.CustomerId, x.BranchId, x.Date!.Value, x.Outstanding)));
        var openOpenings = db.OpeningBalanceEntries.AsNoTracking().GroupBy(e => e.OpeningBalanceId)
            .Select(g => new { Id = g.Key, Outstanding = g.Sum(x => x.Amount) }).Where(x => x.Outstanding > 0);
        var openings = await (from o in openOpenings
                              join b in db.OpeningBalances.AsNoTracking() on o.Id equals b.Id
                              where b.CustomerId != null
                              select new { Customer = b.CustomerId!.Value, b.BranchId, b.OriginalDate, o.Outstanding }).ToArrayAsync(ct);
        items.AddRange(openings.Select(x => (x.Customer, x.BranchId, x.OriginalDate, x.Outstanding)));
        var names = await db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        foreach (var g in items.Where(x => x.Outstanding > 0 && CreditCalculation.Overdue(x.Date, today, days) && branches.ContainsKey(x.Branch)).GroupBy(x => (x.Customer, x.Branch)))
        {
            var name = names.GetValueOrDefault(g.Key.Customer, "Customer");
            desired[$"overdue:{g.Key.Customer}:{g.Key.Branch}"] = new(NotificationKinds.OverdueCredit, Permissions.FinanceView, g.Key.Branch, g.Key.Customer,
                "Overdue credit: " + name, $"{Money(g.Sum(x => x.Outstanding))} unpaid for more than {days} days at {branches[g.Key.Branch]}.");
        }
    }

    // Any earlier day left open is always reported; today's open drawer is reported after the reminder time.
    private async Task RegistersAsync(Dictionary<string, Desired> desired, Dictionary<Guid, string> branches, DateOnly today, bool pastReminder, CancellationToken ct)
    {
        var sessions = await (from r in db.RegisterEvents.AsNoTracking()
                              join a in db.MoneyAccounts.AsNoTracking() on r.AccountId equals a.Id
                              where a.Active
                              group r by new { r.AccountId, a.Name, a.BranchId, r.BusinessDate } into g
                              select new { g.Key.AccountId, g.Key.Name, g.Key.BranchId, g.Key.BusinessDate, Last = g.OrderByDescending(x => x.Sequence).Select(x => x.Kind).First() }).ToArrayAsync(ct);
        foreach (var s in sessions.Where(s => s.Last != "Close" && (s.BusinessDate < today || s.BusinessDate == today && pastReminder) && branches.ContainsKey(s.BranchId)))
            desired[$"register:{s.AccountId}:{s.BusinessDate:yyyy-MM-dd}"] = new(NotificationKinds.RegisterOpen, Permissions.RegisterClose, s.BranchId, s.AccountId,
                $"Close the {s.Name} register", $"{s.Name} at {branches[s.BranchId]} is still open for {s.BusinessDate:yyyy-MM-dd}. Count the cash and close it.");
    }
}

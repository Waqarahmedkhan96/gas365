using System.Globalization;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class ReportAccess
{
    private static readonly string[] Buckets = ["Current", "1–30", "31–60", "61–90", "90+"];

    private async Task<(ReportColumn[], List<object?[]>, object?[])> ExpensesAsync(Guid[] ids, Dictionary<Guid, string> names, ReportFilter f, CancellationToken ct)
    {
        var rows = await (from e in db.FinanceEntries.AsNoTracking()
                          join c in db.ExpenseCategories.AsNoTracking() on e.CategoryId equals c.Id
                          where ids.Contains(e.BranchId) && e.BusinessDate >= f.From && e.BusinessDate <= f.To && (e.Kind == "Expense" || e.Kind == "ExpenseReversal")
                          group e by new { c.Name, c.Capital, e.BranchId } into g
                          select new { g.Key.Name, g.Key.Capital, g.Key.BranchId, Entries = g.Count(x => x.Kind == "Expense"), Amount = -g.Sum(x => x.Cash) }).ToArrayAsync(ct);
        ReportColumn[] columns = [Text("category", "Category"), Text("classification", "Classification"), Text("branch", "Branch"), new("entries", "Entries", "count"), Money("amount", "Amount")];
        var result = rows.Where(x => f.Type is null || (x.Capital ? "Capital" : "Operating") == f.Type).OrderBy(x => x.Name).ThenBy(x => names[x.BranchId])
            .Select(x => new object?[] { x.Name, x.Capital ? "Capital" : "Operating", names[x.BranchId], x.Entries, x.Amount }).ToList();
        return (columns, result, Sum(columns, result, "entries", "amount"));
    }

    // Mirrors the Udhaar view for every party at once: invoice journals age from the invoice date and
    // unapplied advances come from finance entries.
    private async Task<(ReportColumn[], List<object?[]>, object?[])> AgingAsync(Guid[] ids, ReportFilter f, bool supplier, CancellationToken ct)
    {
        var asOf = f.To; var items = new List<(Guid Party, decimal Outstanding, DateOnly Date)>();
        if (supplier)
        {
            var entries = await (from e in db.PurchaseEntries.AsNoTracking()
                                 join d in db.PurchaseDocuments.AsNoTracking() on e.PurchaseId equals d.Id
                                 where ids.Contains(e.BranchId) && e.BusinessDate <= asOf
                                 select new { e.PurchaseId, d.SupplierId, e.Kind, e.Payable, e.BusinessDate }).ToArrayAsync(ct);
            items.AddRange(entries.GroupBy(x => x.PurchaseId).Where(g => g.Any(x => x.Kind == "Invoice"))
                .Select(g => (g.First().SupplierId, g.Sum(x => x.Payable), g.First(x => x.Kind == "Invoice").BusinessDate)));
        }
        else
        {
            var entries = await (from e in db.SaleEntries.AsNoTracking()
                                 join d in db.SaleDocuments.AsNoTracking() on e.SaleId equals d.Id
                                 where ids.Contains(e.BranchId) && e.BusinessDate <= asOf
                                 select new { e.SaleId, d.CustomerId, e.Kind, e.Receivable, e.BusinessDate }).ToArrayAsync(ct);
            items.AddRange(entries.GroupBy(x => x.SaleId).Where(g => g.Any(x => x.Kind == "Invoice"))
                .Select(g => (g.First().CustomerId, g.Sum(x => x.Receivable), g.First(x => x.Kind == "Invoice").BusinessDate)));
            var loans = await (from e in db.CylinderLoanEntries.AsNoTracking()
                               join l in db.CylinderLoans.AsNoTracking() on e.LoanId equals l.Id
                               where ids.Contains(e.BranchId) && e.BusinessDate <= asOf
                               select new { e.LoanId, l.CustomerId, e.Sequence, e.Receivable, e.BusinessDate }).ToArrayAsync(ct);
            items.AddRange(loans.GroupBy(x => x.LoanId).Where(g => g.Any(x => x.Receivable > 0))
                .Select(g => (g.First().CustomerId, g.Sum(x => x.Receivable), g.Where(x => x.Receivable > 0).OrderBy(x => x.Sequence).First().BusinessDate)));
        }
        var unapplied = await db.FinanceEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate <= asOf && (supplier ? x.SupplierId != null : x.CustomerId != null))
            .GroupBy(x => supplier ? x.SupplierId!.Value : x.CustomerId!.Value).Select(g => new { Party = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Party, x => x.Amount, ct);
        var parties = supplier ? await db.Suppliers.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct) : await db.Customers.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        ReportColumn[] columns = [Text("party", supplier ? "Supplier" : "Customer"), .. Buckets.Select((b, i) => Money("b" + i, b)), Money("due", "Total due"), Money("unapplied", "Unapplied advances"), Money("net", "Net balance")];
        var rows = items.Select(x => x.Party).Concat(unapplied.Keys).Distinct().Select(party =>
        {
            var mine = items.Where(x => x.Party == party).ToArray(); var due = mine.Sum(x => x.Outstanding); var advance = unapplied.GetValueOrDefault(party);
            var cells = new List<object?> { parties.GetValueOrDefault(party, "Unknown") };
            cells.AddRange(Buckets.Select(b => (object?)mine.Where(x => FinanceCalculation.AgingBucket(x.Date, asOf) == b).Sum(x => x.Outstanding)));
            cells.AddRange([due, advance, due - advance]);
            return cells.ToArray();
        }).Where(r => r.Skip(1).Any(c => (decimal)c! != 0)).OrderBy(r => (string)r[0]!).ToList();
        return (columns, rows, Sum(columns, rows, [.. columns.Skip(1).Select(c => c.Key)]));
    }

    private async Task<(ReportColumn[], List<object?[]>, object?[])> CashAsync(Guid[] ids, Dictionary<Guid, string> names, ReportFilter f, CancellationToken ct)
    {
        var accounts = await db.MoneyAccounts.AsNoTracking().Where(x => ids.Contains(x.BranchId) && (f.Type == null || x.Kind == f.Type)).ToArrayAsync(ct);
        var accountIds = accounts.Select(x => x.Id).ToArray();
        var entries = await db.FinanceEntries.AsNoTracking().Where(x => x.AccountId != null && accountIds.Contains(x.AccountId.Value) && x.BusinessDate <= f.To)
            .Select(x => new { AccountId = x.AccountId!.Value, x.Kind, x.Cash, x.BusinessDate }).ToArrayAsync(ct);
        var events = await db.RegisterEvents.AsNoTracking().Where(x => accountIds.Contains(x.AccountId) && x.BusinessDate >= f.From && x.BusinessDate <= f.To).ToArrayAsync(ct);
        var categories = ReportCalculation.CashCategories;
        ReportColumn[] columns = [new("date", "Date", "date"), Text("branch", "Branch"), Text("account", "Account"), Money("opening", "Opening"),
            .. categories.Select((c, i) => Money("c" + i, c)), Money("expected", "Expected close"), Money("counted", "Counted"), Money("variance", "Variance"), Text("status", "Register status")];
        var rows = new List<object?[]>();
        foreach (var a in accounts.OrderBy(x => names[x.BranchId]).ThenBy(x => x.Name))
        {
            var mine = entries.Where(x => x.AccountId == a.Id).ToArray(); var sessions = events.Where(x => x.AccountId == a.Id).ToArray();
            var dates = mine.Where(x => x.BusinessDate >= f.From).Select(x => x.BusinessDate).Concat(sessions.Select(x => x.BusinessDate)).Distinct().Order();
            foreach (var date in dates)
            {
                // Matches the register: opening is all earlier cash plus the account's own opening entry on that date.
                var opening = mine.Where(x => x.BusinessDate < date || x.BusinessDate == date && x.Kind == "Opening").Sum(x => x.Cash);
                var day = mine.Where(x => x.BusinessDate == date && x.Kind != "Opening").ToArray();
                var lines = categories.Select(c => day.Where(x => ReportCalculation.CashCategory(x.Kind) == c).Sum(x => x.Cash)).ToArray();
                var last = sessions.Where(x => x.BusinessDate == date).OrderBy(x => x.Sequence).LastOrDefault();
                var status = a.Kind != "Cash" ? "No register" : last is null ? "Not opened" : last.Kind == "Close" ? "Closed" : "Open";
                rows.Add([Date(date), names[a.BranchId], a.Name, opening, .. lines.Cast<object?>(), opening + day.Sum(x => x.Cash),
                    last?.Kind == "Close" ? last.Counted : null, last?.Kind == "Close" ? last.Counted - last.Expected : null, status]);
            }
        }
        return (columns, rows, Sum(columns, rows, [.. categories.Select((_, i) => "c" + i), "variance"]));
    }

    private async Task<(ReportColumn[], List<object?[]>, object?[])> ActivityAsync(ReportFilter f, CancellationToken ct)
    {
        var zone = await ZoneAsync(ct);
        // Local midnight boundaries converted to UTC, which is what PostgreSQL timestamptz parameters require.
        DateTimeOffset Start(DateOnly date) { var local = date.ToDateTime(TimeOnly.MinValue); return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime(); }
        var from = Start(f.From); var to = Start(f.To.AddDays(1));
        var events = await db.AuditEvents.AsNoTracking().Where(x => x.OccurredAt >= from && x.OccurredAt < to)
            .GroupBy(x => new { x.ActorId, x.Action }).Select(g => new { g.Key.ActorId, g.Key.Action, Count = g.Count(), First = g.Min(x => x.OccurredAt), Last = g.Max(x => x.OccurredAt) }).ToArrayAsync(ct);
        if (f.Type is not null) events = [.. events.Where(x => ActivityAreas[f.Type].Contains(x.Action.Split('.')[0]))];
        var actorIds = events.Where(x => x.ActorId.HasValue).Select(x => x.ActorId!.Value).Distinct().ToArray();
        // Only people who are or were members of this tenant are named; the user table is global.
        var members = await db.Memberships.AsNoTracking().Where(m => actorIds.Contains(m.UserId)).Select(m => m.UserId).ToArrayAsync(ct);
        var people = await db.Users.AsNoTracking().Where(u => members.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email ?? "Member" : u.DisplayName, ct);
        string Local(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        ReportColumn[] columns = [Text("user", "User"), Text("action", "Action"), new("count", "Count", "count"), Text("first", "First (local)"), Text("last", "Last (local)")];
        var rows = events.Select(x => new { User = x.ActorId.HasValue ? people.GetValueOrDefault(x.ActorId.Value, "Former member") : "System", x.Action, x.Count, x.First, x.Last })
            .OrderBy(x => x.User).ThenBy(x => x.Action).Select(x => new object?[] { x.User, x.Action, x.Count, Local(x.First), Local(x.Last) }).ToList();
        return (columns, rows, Sum(columns, rows, "count"));
    }
}

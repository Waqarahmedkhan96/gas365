using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed class AuditAccess(Gas365DbContext db, TenantScope scope) : IAuditAccess
{
    private const int PageSize = 20;
    internal static readonly string[] Modules = ["stock", "sales", "purchasing", "finance", "cylinders", "openings"];

    private void Validate(DateOnly from, DateOnly to, int page)
    {
        if (!scope.Has(Permissions.AuditView)) throw new AccessException(403, "Audit permission required.");
        if (from > to || to.DayNumber - from.DayNumber >= 366) throw new AccessException(400, "Choose a period of at most 366 days.");
        if (page is < 1 or > 100_000) throw new AccessException(400, "Invalid page.");
    }

    private async Task<Dictionary<Guid, string>> PeopleAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToArray();
        var members = await db.Memberships.AsNoTracking().Where(m => wanted.Contains(m.UserId)).Select(m => m.UserId).ToArrayAsync(ct);
        return await db.Users.AsNoTracking().Where(u => members.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email ?? "Member" : u.DisplayName, ct);
    }

    private async Task<(DateTimeOffset From, DateTimeOffset To)> UtcRangeAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById((await db.Tenants.AsNoTracking().SingleAsync(ct)).TimeZone);
        DateTimeOffset Start(DateOnly d) { var local = d.ToDateTime(TimeOnly.MinValue); return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime(); }
        return (Start(from), Start(to.AddDays(1)));
    }

    private static string Area(string action)
    {
        var prefix = action.Split('.')[0];
        return ReportAccess.ActivityAreas.FirstOrDefault(a => a.Value.Contains(prefix)).Key ?? "other";
    }

    public async Task<PageDto<AuditEntryDto>> TimelineAsync(DateOnly from, DateOnly to, string? area, Guid? actorId, Guid? subjectId, int page, CancellationToken ct)
    {
        Validate(from, to, page);
        if (area is not null && !ReportAccess.ActivityAreas.ContainsKey(area)) throw new AccessException(400, "Unknown activity area.");
        var (start, end) = await UtcRangeAsync(from, to, ct);
        var query = db.AuditEvents.AsNoTracking().Where(x => x.OccurredAt >= start && x.OccurredAt < end);
        if (area is not null)
        {
            var prefixes = ReportAccess.ActivityAreas[area];
            query = query.Where(x => prefixes.Contains(x.Action.Substring(0, x.Action.IndexOf("."))));
        }
        if (actorId.HasValue) query = query.Where(x => x.ActorId == actorId);
        if (subjectId.HasValue) query = query.Where(x => x.SubjectId == subjectId);
        var rows = await query.OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Id).Skip((page - 1) * PageSize).Take(PageSize).ToArrayAsync(ct);
        var people = await PeopleAsync(rows.Where(x => x.ActorId.HasValue).Select(x => x.ActorId!.Value), ct);
        return new(rows.Select(x => new AuditEntryDto(x.Id, x.Action, Area(x.Action), x.ActorId,
            x.ActorId.HasValue ? people.GetValueOrDefault(x.ActorId.Value, "Former member") : "System", x.SubjectId, x.OccurredAt)).ToArray(),
            await query.CountAsync(ct), page, PageSize);
    }

    private sealed record Row(string Module, string Kind, Guid OriginalId, Guid ReversalId, Guid BranchId, DateOnly BusinessDate, decimal Amount, string Reason, Guid ActorId, DateTimeOffset PostedAt);

    // One view of every correction across the append-only ledgers, newest first.
    public async Task<PageDto<CorrectionDto>> CorrectionsAsync(DateOnly from, DateOnly to, Guid? branchId, string? module, int page, CancellationToken ct)
    {
        Validate(from, to, page);
        if (module is not null && !Modules.Contains(module)) throw new AccessException(400, "Unknown module.");
        var branches = await BranchScope.Accessible(db, scope).Where(b => !branchId.HasValue || b.Id == branchId).ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        if (branchId.HasValue && branches.Count == 0) throw new AccessException(404, "Branch not found.");
        var ids = branches.Keys.ToArray(); var rows = new List<Row>();
        bool Want(string m) => module is null || module == m;
        if (Want("stock"))
            rows.AddRange(await (from d in db.StockDocuments.AsNoTracking()
                                 where d.ReversesId != null && ids.Contains(d.BranchId) && d.BusinessDate >= @from && d.BusinessDate <= to
                                 select new Row("stock", d.Kind, d.ReversesId!.Value, d.Id, d.BranchId, d.BusinessDate,
                                     db.StockMovements.Where(m => m.DocumentId == d.Id).Sum(m => m.Value), d.Reason, d.ActorId, d.PostedAt)).ToArrayAsync(ct));
        if (Want("sales"))
            rows.AddRange(await db.SaleEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= from && x.BusinessDate <= to && x.Kind.EndsWith("Reversal"))
                .Select(x => new Row("sales", x.Kind, x.ReversesId ?? x.SaleId, x.Id, x.BranchId, x.BusinessDate, x.Kind == "PaymentReversal" ? x.Cash : x.Kind == "Reversal" ? x.Revenue : x.Receivable, x.Reason, x.ActorId, x.PostedAt)).ToArrayAsync(ct));
        if (Want("purchasing"))
            rows.AddRange(await db.PurchaseEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= from && x.BusinessDate <= to && x.Kind.EndsWith("Reversal"))
                .Select(x => new Row("purchasing", x.Kind, x.ReversesId ?? x.PurchaseId, x.Id, x.BranchId, x.BusinessDate, x.Cash != 0 ? x.Cash : x.Payable, x.Reason, x.ActorId, x.PostedAt)).ToArrayAsync(ct));
        if (Want("finance"))
            rows.AddRange(await db.FinanceEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= from && x.BusinessDate <= to && x.ReversesId != null)
                .Select(x => new Row("finance", x.Kind, x.ReversesId!.Value, x.Id, x.BranchId, x.BusinessDate, x.Cash != 0 ? x.Cash : x.Amount, x.Reason, x.ActorId, x.PostedAt)).ToArrayAsync(ct));
        if (Want("cylinders"))
            rows.AddRange(await db.CylinderLoanEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= from && x.BusinessDate <= to && x.ReversesId != null)
                .Select(x => new Row("cylinders", x.Kind, x.ReversesId!.Value, x.Id, x.BranchId, x.BusinessDate, x.Cash != 0 ? x.Cash : x.Receivable != 0 ? x.Receivable : x.Revenue, x.Reason, x.ActorId, x.PostedAt)).ToArrayAsync(ct));
        if (Want("openings"))
            rows.AddRange(await db.OpeningBalanceEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= from && x.BusinessDate <= to && x.ReversesId != null)
                .Select(x => new Row("openings", x.Kind, x.ReversesId!.Value, x.Id, x.BranchId, x.BusinessDate, x.Amount, x.Reason, x.ActorId, x.PostedAt)).ToArrayAsync(ct));
        var ordered = rows.OrderByDescending(x => x.PostedAt).ThenBy(x => x.ReversalId).ToArray();
        var pageRows = ordered.Skip((page - 1) * PageSize).Take(PageSize).ToArray();
        var people = await PeopleAsync(pageRows.Select(x => x.ActorId), ct);
        return new(pageRows.Select(x => new CorrectionDto(x.Module, x.Kind, x.OriginalId, x.ReversalId, branches[x.BranchId], x.BusinessDate, x.Amount, x.Reason,
            people.GetValueOrDefault(x.ActorId, "Former member"), x.PostedAt)).ToArray(), ordered.Length, page, PageSize);
    }
}

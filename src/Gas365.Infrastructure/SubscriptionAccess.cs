using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Gas365.Infrastructure;

// The platform administrator is identified by a server-side email list, never by anything the browser sends.
public static class PlatformAdmins
{
    public static bool Contains(IConfiguration configuration, string? email) =>
        email is not null && (configuration["Platform:AdminEmails"] ?? "")
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(email, StringComparer.OrdinalIgnoreCase);
}

public sealed class ManualPaymentProvider : IPaymentProvider
{
    public string Name => "Manual";
    private static string Amount(decimal value) => value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

    public PaymentStartDto Start(string businessName, PlanDto plan, string billingPeriod)
    {
        var yearly = billingPeriod == SubscriptionRules.Yearly;
        var price = yearly ? plan.YearlyPrice : plan.MonthlyPrice;
        var due = price is null ? "" : yearly
            ? $" The amount due is PKR {Amount(price.Value)} for 12 months (two months free)."
            : $" The amount due is PKR {Amount(price.Value)} for one month.";
        return new(Name,
            $"Your request for the {plan.Name} plan, billed {billingPeriod.ToLowerInvariant()}, has been sent to the Gas365 team.{due} Pay by bank transfer quoting \"{businessName}\"; "
            + "the plan is activated once the payment is confirmed. Your data stays available meanwhile.");
    }
}

public sealed class SubscriptionAccess(Gas365DbContext db, TenantScope scope, IPaymentProvider payments) : ISubscriptionAccess
{
    internal static PlanDto Dto(Plan p) => new(p.Id, p.Code, p.Name, p.MonthlyPrice, p.MaxBranches, p.MaxUsers, p.MaxAdmins, p.Active, p.Version,
        SubscriptionRules.YearlyPrice(p.MonthlyPrice), SubscriptionRules.YearlySaving(p.MonthlyPrice));
    private void Require(string permission) { if (!scope.Has(permission)) throw new AccessException(403, "Only the business owner can manage the subscription."); }
    private void Audit(string action, Guid? subject) => db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId, ActorId = scope.UserId, Action = action, SubjectId = subject });

    internal static async Task<UsageDto> UsageAsync(Gas365DbContext db, Tenant tenant, CancellationToken ct)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone);
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        var first = new DateOnly(local.Year, local.Month, 1); var last = first.AddMonths(1).AddDays(-1);
        var startLocal = first.ToDateTime(TimeOnly.MinValue);
        var start = new DateTimeOffset(startLocal, zone.GetUtcOffset(startLocal)).ToUniversalTime();
        var t = tenant.TenantId;
        var roles = await db.TenantRoles.IgnoreQueryFilters().Where(r => r.TenantId == t).ToDictionaryAsync(r => r.Id, ct);
        var members = await db.Memberships.IgnoreQueryFilters().Where(m => m.TenantId == t && m.Active).Select(m => m.RoleId).ToArrayAsync(ct);
        return new(first.ToString("yyyy-MM"),
            await db.Branches.IgnoreQueryFilters().CountAsync(b => b.TenantId == t && b.Active, ct),
            members.Length,
            members.Count(r => roles[r].IsOwner || roles[r].Permissions.Contains(Permissions.TeamManage) || roles[r].Permissions.Contains(Permissions.RoleManage)),
            await db.SaleEntries.IgnoreQueryFilters().CountAsync(x => x.TenantId == t && x.Kind == "Invoice" && x.BusinessDate >= first && x.BusinessDate <= last, ct),
            await db.PurchaseEntries.IgnoreQueryFilters().CountAsync(x => x.TenantId == t && x.Kind == "Invoice" && x.BusinessDate >= first && x.BusinessDate <= last, ct),
            await db.FinanceEntries.IgnoreQueryFilters().CountAsync(x => x.TenantId == t && !x.IdempotencyKey.StartsWith("internal:") && x.BusinessDate >= first && x.BusinessDate <= last, ct),
            await db.StockDocuments.IgnoreQueryFilters().CountAsync(x => x.TenantId == t && x.BusinessDate >= first && x.BusinessDate <= last, ct),
            await db.ExportJobs.IgnoreQueryFilters().CountAsync(x => x.TenantId == t && x.RequestedAt >= start, ct),
            await db.ImportRows.IgnoreQueryFilters().CountAsync(x => x.TenantId == t && x.Status == ImportStatus.Committed
                && db.ImportBatches.IgnoreQueryFilters().Any(b => b.TenantId == t && b.Id == x.BatchId && b.CommittedAt >= start), ct),
            await db.ExportJobs.IgnoreQueryFilters().Where(x => x.TenantId == t && x.Content != null).SumAsync(x => (long)x.Content!.Length, ct)
                + await db.ImportRows.IgnoreQueryFilters().Where(x => x.TenantId == t).SumAsync(x => (long)x.Data.Length, ct));
    }

    public async Task<SubscriptionDto> SubscriptionAsync(CancellationToken ct)
    {
        Require(Permissions.TenantManage);
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(ct);
        var plans = await db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ToArrayAsync(ct);
        var names = plans.ToDictionary(p => p.Id, p => p.Name);
        var history = await db.SubscriptionEvents.AsNoTracking().OrderByDescending(x => x.OccurredAt).Take(50).ToArrayAsync(ct);
        return new(plans.Where(p => p.Id == tenant.PlanId).Select(Dto).SingleOrDefault(),
            SubscriptionRules.Effective(tenant.SubscriptionStatus, tenant.TrialEndsAt, tenant.PaidThrough, DateTimeOffset.UtcNow),
            tenant.TrialEndsAt, tenant.PaidThrough, plans.Where(p => p.Id == tenant.RequestedPlanId).Select(Dto).SingleOrDefault(),
            await UsageAsync(db, tenant, ct), plans.Where(p => p.Active).Select(Dto).ToArray(),
            history.Select(h => new SubscriptionEventDto(h.Id, h.Kind, h.PlanId.HasValue ? names.GetValueOrDefault(h.PlanId.Value) : null, h.Amount, h.Reference, h.Note, h.PeriodEnd, h.OccurredAt)).ToArray(),
            payments.Name, tenant.RequestedPlanId is null ? null : tenant.RequestedBillingPeriod ?? SubscriptionRules.Monthly);
    }

    public async Task<PaymentStartDto> RequestPlanAsync(PlanRequest request, CancellationToken ct)
    {
        Require(Permissions.TenantManage);
        string period;
        try { period = SubscriptionRules.Period(request.BillingPeriod); }
        catch (ArgumentException e) { throw new AccessException(400, e.Message); }
        var plan = await db.Plans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.PlanId && p.Active, ct) ?? throw new AccessException(404, "Plan not found.");
        var tenant = await db.Tenants.SingleAsync(ct);
        tenant.RequestedPlanId = plan.Id; tenant.RequestedBillingPeriod = period;
        db.SubscriptionEvents.Add(new SubscriptionEvent { TenantId = tenant.TenantId, Kind = "PlanRequested", PlanId = plan.Id, ActorId = scope.UserId, Note = $"Awaiting payment confirmation · {period} billing" });
        Audit("subscription.plan_requested", plan.Id); await db.SaveChangesAsync(ct);
        return payments.Start(tenant.Name, Dto(plan), period);
    }

    private static SupportGrantDto Dto(SupportGrant g, string tenantName) =>
        new(g.Id, g.TenantId, tenantName, g.IncludeFinance, g.Reason, g.CreatedAt, g.ExpiresAt, g.RevokedAt is null && g.ExpiresAt > DateTimeOffset.UtcNow);

    public async Task<SupportGrantDto[]> SupportGrantsAsync(CancellationToken ct)
    {
        Require(Permissions.TenantManage);
        var name = (await db.Tenants.AsNoTracking().SingleAsync(ct)).Name;
        return (await db.SupportGrants.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(20).ToArrayAsync(ct)).Select(g => Dto(g, name)).ToArray();
    }

    public async Task<SupportGrantDto> GrantSupportAsync(SupportGrantRequest request, CancellationToken ct)
    {
        Require(Permissions.TenantManage);
        if (request.Hours is < 1 or > 72) throw new AccessException(400, "Grant support access for 1 to 72 hours.");
        var now = DateTimeOffset.UtcNow;
        var grant = new SupportGrant { TenantId = scope.TenantId, GrantedBy = scope.UserId, IncludeFinance = request.IncludeFinance, Reason = CatalogAccess.Text(request.Reason, 300), CreatedAt = now, ExpiresAt = now.AddHours(request.Hours) };
        db.SupportGrants.Add(grant); Audit(request.IncludeFinance ? "support.granted_with_finance" : "support.granted", grant.Id);
        await db.SaveChangesAsync(ct);
        return Dto(grant, (await db.Tenants.AsNoTracking().SingleAsync(ct)).Name);
    }

    public async Task RevokeSupportAsync(Guid id, CancellationToken ct)
    {
        Require(Permissions.TenantManage);
        var grant = await db.SupportGrants.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Support grant not found.");
        if (grant.RevokedAt is not null) return;
        grant.RevokedAt = DateTimeOffset.UtcNow; grant.RevokedBy = scope.UserId;
        Audit("support.revoked", id); await db.SaveChangesAsync(ct);
    }

    // Computed from real data, so steps complete themselves as the business is set up.
    public async Task<OnboardingDto> OnboardingAsync(CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(ct);
        var status = SubscriptionRules.Effective(tenant.SubscriptionStatus, tenant.TrialEndsAt, tenant.PaidThrough, DateTimeOffset.UtcNow);
        OnboardingStepDto[] steps =
        [
            new("warehouse", "Add a warehouse for your stock", "inventory", await db.Warehouses.AnyAsync(ct)),
            new("items", "Add LPG, cylinder and spare-part items", "inventory", await db.StockItems.AnyAsync(ct)),
            new("stock", "Record opening stock", "inventory", await db.StockDocuments.AnyAsync(ct)),
            new("customers", "Add customers or import them", "customers", await db.Customers.AnyAsync(ct)),
            new("prices", "Set selling prices", "prices", await db.SalePrices.AnyAsync(ct)),
            new("cash", "Create a cash drawer and open the register", "cash-register", await db.MoneyAccounts.AnyAsync(ct)),
            new("team", "Invite your team", "team", await db.Memberships.CountAsync(ct) > 1),
            new("plan", "Choose a plan", "subscription", status == SubscriptionRules.Active)
        ];
        return new(tenant.OnboardingDismissed, steps);
    }

    public async Task SetOnboardingAsync(OnboardingRequest request, CancellationToken ct)
    {
        Require(Permissions.TenantManage);
        var tenant = await db.Tenants.SingleAsync(ct); tenant.OnboardingDismissed = request.Dismissed; await db.SaveChangesAsync(ct);
    }

    public async Task EnsureWritableAsync(string path, CancellationToken ct)
    {
        if (SubscriptionRules.AllowedWhenExpired(path)) return;
        var tenant = await db.Tenants.AsNoTracking().Select(t => new { t.SubscriptionStatus, t.TrialEndsAt, t.PaidThrough }).SingleAsync(ct);
        if (SubscriptionRules.Effective(tenant.SubscriptionStatus, tenant.TrialEndsAt, tenant.PaidThrough, DateTimeOffset.UtcNow) == SubscriptionRules.Expired)
            throw new AccessException(402, "Your trial or plan has ended. Your data is safe and still readable; choose a plan under Subscription to make changes again.");
    }

    public async Task RecordSupportViewAsync(CancellationToken ct)
    {
        db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId, ActorId = scope.UserId, Action = "support.viewed", SubjectId = scope.SupportGrantId });
        await db.SaveChangesAsync(ct);
    }
}

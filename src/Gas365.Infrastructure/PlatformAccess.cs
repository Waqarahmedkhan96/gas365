using Gas365.Application;
using Gas365.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Gas365.Infrastructure;

public sealed class PlatformAccess(Gas365DbContext db, TenantScope scope, UserManager<ApplicationUser> users, IConfiguration configuration, IPaymentProvider payments) : IPlatformAccess
{
    private async Task RequireAdminAsync()
    {
        var user = await users.FindByIdAsync(scope.UserId.ToString());
        if (user is null || !user.EmailConfirmed || !PlatformAdmins.Contains(configuration, user.Email))
            throw new AccessException(403, "Platform administration is restricted.");
    }

    // Platform actions are written into the affected business's audit log so its owner can see them.
    private void AuditIn(Guid tenantId, string action, Guid? subject)
    {
        scope.TenantId = tenantId;
        db.AuditEvents.Add(new AuditEvent { TenantId = tenantId, ActorId = scope.UserId, Action = action, SubjectId = subject });
    }

    private async Task<TenantSummaryDto> SummaryAsync(Tenant t, Dictionary<Guid, string> plans, CancellationToken ct)
    {
        var usage = await SubscriptionAccess.UsageAsync(db, t, ct);
        return new(t.TenantId, t.Name, t.PlanId.HasValue ? plans.GetValueOrDefault(t.PlanId.Value) : null,
            SubscriptionRules.Effective(t.SubscriptionStatus, t.TrialEndsAt, t.PaidThrough, DateTimeOffset.UtcNow), t.TrialEndsAt, t.PaidThrough,
            t.RequestedPlanId.HasValue ? plans.GetValueOrDefault(t.RequestedPlanId.Value) : null, usage.Users, usage.Branches,
            usage.SalesInvoices + usage.PurchaseInvoices + usage.FinanceEntries + usage.StockDocuments, t.CreatedAt,
            t.RequestedPlanId.HasValue ? t.RequestedBillingPeriod ?? SubscriptionRules.Monthly : null);
    }

    public async Task<PageDto<TenantSummaryDto>> TenantsAsync(string? search, int page, CancellationToken ct)
    {
        await RequireAdminAsync();
        if (page is < 1 or > 100_000) throw new AccessException(400, "Invalid page.");
        var query = db.Tenants.IgnoreQueryFilters().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(t => EF.Functions.ILike(t.Name, "%" + search.Trim() + "%"));
        // Businesses awaiting activation first, then the newest.
        var rows = await query.OrderByDescending(t => t.RequestedPlanId != null).ThenByDescending(t => t.CreatedAt).Skip((page - 1) * 20).Take(20).ToArrayAsync(ct);
        var plans = await db.Plans.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var items = new List<TenantSummaryDto>(); foreach (var t in rows) items.Add(await SummaryAsync(t, plans, ct));
        return new(items.ToArray(), await query.CountAsync(ct), page, 20);
    }

    public async Task<PlanDto[]> PlansAsync(CancellationToken ct)
    {
        await RequireAdminAsync();
        return (await db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ToArrayAsync(ct)).Select(SubscriptionAccess.Dto).ToArray();
    }

    public async Task<PlanDto> UpdatePlanAsync(Guid id, PlanUpdateRequest r, CancellationToken ct)
    {
        await RequireAdminAsync();
        var plan = await db.Plans.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new AccessException(404, "Plan not found.");
        if (plan.Version != r.Version) throw new AccessException(409, "The plan changed. Refresh first.");
        if (r.MaxBranches is < 1 or > 1000 || r.MaxUsers is < 1 or > 10000 || r.MaxAdmins < 1 || r.MaxAdmins > r.MaxUsers) throw new AccessException(400, "Enter valid plan limits.");
        if (r.MonthlyPrice is < 0) throw new AccessException(400, "Price cannot be negative.");
        if (r.MonthlyPrice.HasValue) CatalogAccess.Decimal(r.MonthlyPrice.Value, 2, 100000000);
        plan.Name = CatalogAccess.Text(r.Name, 60); plan.MonthlyPrice = r.MonthlyPrice; plan.MaxBranches = r.MaxBranches; plan.MaxUsers = r.MaxUsers; plan.MaxAdmins = r.MaxAdmins; plan.Active = r.Active;
        await db.SaveChangesAsync(ct);
        return SubscriptionAccess.Dto(plan);
    }

    // Records a confirmed payment. Limits follow the plan; a business above a lower plan's limits keeps its data
    // but cannot add more branches or users until it is within the limit.
    public async Task<TenantSummaryDto> ActivateAsync(Guid tenantId, ActivationRequest r, CancellationToken ct)
    {
        await RequireAdminAsync();
        if (r.Months is < 1 or > 24) throw new AccessException(400, "Activate for 1 to 24 months.");
        CatalogAccess.Decimal(r.Amount, 2, 100000000); if (r.Amount < 0) throw new AccessException(400, "Amount cannot be negative.");
        var plan = await db.Plans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == r.PlanId && p.Active, ct) ?? throw new AccessException(404, "Plan not found.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(t => t.TenantId == tenantId, ct) ?? throw new AccessException(404, "Business not found.");
        AuditIn(tenantId, "subscription.activated", plan.Id);
        var now = DateTimeOffset.UtcNow;
        var start = tenant.SubscriptionStatus == SubscriptionRules.Active && tenant.PaidThrough > now ? tenant.PaidThrough.Value : now;
        tenant.PlanId = plan.Id; tenant.SubscriptionStatus = SubscriptionRules.Active; tenant.PaidThrough = start.AddMonths(r.Months); tenant.RequestedPlanId = null; tenant.RequestedBillingPeriod = null;
        tenant.MaxBranches = plan.MaxBranches; tenant.MaxUsers = plan.MaxUsers; tenant.MaxAdmins = plan.MaxAdmins;
        db.SubscriptionEvents.Add(new SubscriptionEvent
        {
            TenantId = tenantId,
            Kind = "Activated",
            PlanId = plan.Id,
            Amount = r.Amount,
            Reference = CatalogAccess.Text(r.Reference, 120),
            Note = $"{payments.Name} payment confirmed for {r.Months} month(s)",
            PeriodEnd = tenant.PaidThrough,
            ActorId = scope.UserId
        });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await SummaryAsync(tenant, await db.Plans.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct), ct);
    }

    public async Task<TenantSummaryDto> ExtendTrialAsync(Guid tenantId, TrialExtensionRequest r, CancellationToken ct)
    {
        await RequireAdminAsync();
        if (r.Days is < 1 or > 30) throw new AccessException(400, "Extend a trial by 1 to 30 days.");
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(t => t.TenantId == tenantId, ct) ?? throw new AccessException(404, "Business not found.");
        if (tenant.SubscriptionStatus != SubscriptionRules.Trial) throw new AccessException(409, "Only trials can be extended.");
        AuditIn(tenantId, "subscription.trial_extended", tenantId);
        var now = DateTimeOffset.UtcNow;
        tenant.TrialEndsAt = (tenant.TrialEndsAt > now ? tenant.TrialEndsAt.Value : now).AddDays(r.Days);
        db.SubscriptionEvents.Add(new SubscriptionEvent { TenantId = tenantId, Kind = "TrialExtended", PlanId = tenant.PlanId, Note = CatalogAccess.Text(r.Reason, 300), PeriodEnd = tenant.TrialEndsAt, ActorId = scope.UserId });
        await db.SaveChangesAsync(ct);
        return await SummaryAsync(tenant, await db.Plans.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct), ct);
    }

    public async Task<SupportGrantDto[]> SupportGrantsAsync(CancellationToken ct)
    {
        await RequireAdminAsync();
        var now = DateTimeOffset.UtcNow;
        return await (from g in db.SupportGrants.IgnoreQueryFilters().AsNoTracking()
                      join t in db.Tenants.IgnoreQueryFilters() on g.TenantId equals t.TenantId
                      where g.RevokedAt == null && g.ExpiresAt > now
                      orderby g.ExpiresAt
                      select new SupportGrantDto(g.Id, g.TenantId, t.Name, g.IncludeFinance, g.Reason, g.CreatedAt, g.ExpiresAt, true)).ToArrayAsync(ct);
    }

    // Switches only the current session into the business's read-only support view.
    public async Task EnterSupportAsync(Guid grantId, CancellationToken ct)
    {
        await RequireAdminAsync();
        var now = DateTimeOffset.UtcNow;
        var grant = await db.SupportGrants.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(g => g.Id == grantId && g.RevokedAt == null && g.ExpiresAt > now, ct)
            ?? throw new AccessException(404, "No active support grant.");
        await db.LoginSessions.IgnoreQueryFilters().Where(s => s.Id == scope.SessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.SupportGrantId, grant.Id), ct);
        AuditIn(grant.TenantId, "support.entered", grant.Id); await db.SaveChangesAsync(ct);
    }

    public async Task ExitSupportAsync(CancellationToken ct)
    {
        var grantId = scope.SupportGrantId;
        await db.LoginSessions.IgnoreQueryFilters().Where(s => s.Id == scope.SessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.SupportGrantId, (Guid?)null), ct);
        if (grantId.HasValue) { AuditIn(scope.TenantId, "support.exited", grantId); await db.SaveChangesAsync(ct); }
    }
}

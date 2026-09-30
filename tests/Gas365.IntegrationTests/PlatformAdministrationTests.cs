using System.Net;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.IntegrationTests;

public sealed partial class CustomerTests
{
    private static Task<HttpResponseMessage> Platform((HttpClient Client, MeDto Me) o, string path, object? body = null) =>
        Send(o.Client, "/api/platform" + path, body is null ? HttpMethod.Get : HttpMethod.Post, body);
    private static Task<HttpResponseMessage> Workspace((HttpClient Client, MeDto Me) o, string path, object? body = null, HttpMethod? method = null, Guid? tenant = null) =>
        Send(o.Client, "/api/workspace" + path, method ?? (body is null ? HttpMethod.Get : HttpMethod.Post), body, tenant ?? o.Me.TenantId);

    [Fact]
    public async Task NewBusinessesTrialThenBecomeReadOnlyButCanExportChoosePlansAndGrantSupport()
    {
        var o = await Owner(); using var client = o.Client;
        var subscription = await Read<SubscriptionDto>(await Workspace(o, "/subscription"));
        Assert.Equal(SubscriptionRules.Trial, subscription.Status); Assert.Equal("business", subscription.Plan!.Code);
        Assert.InRange(subscription.TrialEndsAt!.Value, DateTimeOffset.UtcNow.AddDays(13.9), DateTimeOffset.UtcNow.AddDays(14.1));
        Assert.Equal(3, subscription.Plans.Length); Assert.Contains(subscription.History, h => h.Kind == "TrialStarted");
        Assert.Equal(HttpStatusCode.OK, (await Sales(o, "customers", new CustomerRequest("Trial customer", "", ""))).StatusCode);

        await using var db = database.CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Tenants\" SET \"TrialEndsAt\" = now() - interval '1 minute' WHERE \"TenantId\" = {o.Me.TenantId}");
        var blocked = await Sales(o, "customers", new CustomerRequest("Blocked", "", ""));
        Assert.Equal(HttpStatusCode.PaymentRequired, blocked.StatusCode); Assert.Contains("data is safe", await blocked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await Sales(o, "customers?page=1")).StatusCode);
        Assert.Equal(SubscriptionRules.Expired, (await Read<TenantDto>(await Workspace(o, "/tenant"))).SubscriptionStatus);
        Assert.Equal(HttpStatusCode.OK, (await Reports(o, "/exports", new ExportRequest("daily-sales", "csv", null, Today(), Today(), null, Key()))).StatusCode);
        var payment = await Read<PaymentStartDto>(await Workspace(o, "/subscription/request", new PlanRequest(SubscriptionRules.StarterId)));
        Assert.Equal("Manual", payment.Provider); Assert.Contains("bank transfer", payment.Instructions);
        Assert.Equal(HttpStatusCode.OK, (await Workspace(o, "/support", new SupportGrantRequest(4, false, "Help while expired"))).StatusCode);
        Assert.Equal("starter", (await Read<SubscriptionDto>(await Workspace(o, "/subscription"))).RequestedPlan!.Code);
    }

    [Fact]
    public async Task PlatformAdministratorActivatesPlansExtendsTrialsAndEveryActionIsVisibleToTheOwner()
    {
        var owner = await Owner(); using var oc = owner.Client;
        var admin = await Owner("admin-platform@test.example"); using var ac = admin.Client;
        Assert.True(admin.Me.PlatformAdmin); Assert.False(owner.Me.PlatformAdmin);
        Assert.Equal(HttpStatusCode.Forbidden, (await Platform(owner, "/tenants")).StatusCode);
        await Read<PaymentStartDto>(await Workspace(owner, "/subscription/request", new PlanRequest(SubscriptionRules.StarterId)));
        var tenants = await Read<PageDto<TenantSummaryDto>>(await Platform(admin, "/tenants?search=Inventory"));
        var summary = tenants.Items.First(); Assert.Equal(owner.Me.TenantId, summary.TenantId); Assert.Equal("Starter", summary.RequestedPlan);

        var extended = await Read<TenantSummaryDto>(await Platform(admin, $"/tenants/{owner.Me.TenantId}/extend-trial", new TrialExtensionRequest(7, "Migrating from Excel")));
        Assert.True(extended.TrialEndsAt > DateTimeOffset.UtcNow.AddDays(20));
        var active = await Read<TenantSummaryDto>(await Platform(admin, $"/tenants/{owner.Me.TenantId}/activate", new ActivationRequest(SubscriptionRules.StarterId, 1, 4999, "Bank ref 7781")));
        Assert.Equal(SubscriptionRules.Active, active.Status); Assert.Equal("Starter", active.Plan); Assert.Null(active.RequestedPlan);
        Assert.Equal(HttpStatusCode.Conflict, (await Platform(admin, $"/tenants/{owner.Me.TenantId}/extend-trial", new TrialExtensionRequest(3, "Too late"))).StatusCode);
        var tenant = await Read<TenantDto>(await Workspace(owner, "/tenant")); Assert.Equal(1, tenant.MaxBranches); Assert.Equal(3, tenant.MaxUsers);
        Assert.Equal(HttpStatusCode.Conflict, (await Workspace(owner, "/branches", new BranchRequest("Second branch"))).StatusCode);
        var history = (await Read<SubscriptionDto>(await Workspace(owner, "/subscription"))).History;
        Assert.Contains(history, h => h.Kind == "Activated" && h.Amount == 4999 && h.Reference == "Bank ref 7781" && h.PlanName == "Starter");
        await using var db = database.CreateContext();
        var audit = await db.AuditEvents.IgnoreQueryFilters().Where(x => x.TenantId == owner.Me.TenantId && x.ActorId == admin.Me.UserId).Select(x => x.Action).ToArrayAsync();
        Assert.Contains("subscription.activated", audit); Assert.Contains("subscription.trial_extended", audit);

        var plans = await Read<PlanDto[]>(await Platform(admin, "/plans"));
        var starter = plans.Single(p => p.Code == "starter");
        var priced = await Read<PlanDto>(await Send(admin.Client, $"/api/platform/plans/{starter.Id}", HttpMethod.Put, new PlanUpdateRequest("Starter", 2500, 1, 3, 1, true, starter.Version)));
        Assert.Equal(2500, priced.MonthlyPrice);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(admin.Client, $"/api/platform/plans/{starter.Id}", HttpMethod.Put, new PlanUpdateRequest("Starter", 1, 1, 3, 1, true, starter.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin.Client, $"/api/platform/plans/{starter.Id}", HttpMethod.Put, new PlanUpdateRequest("Starter", 1, 1, 3, 5, true, priced.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(owner.Client, $"/api/platform/plans/{starter.Id}", HttpMethod.Put, new PlanUpdateRequest("Free", 0, 9, 9, 9, true, priced.Version))).StatusCode);
    }

    [Fact]
    public async Task PublicPlanCatalogueIsAnonymousReadOnlyAndCarriesServerYearlyPrices()
    {
        using var client = Client();
        var plans = await Read<PlanDto[]>(await client.GetAsync("/api/public/plans"));
        Assert.NotEmpty(plans);
        Assert.All(plans, p => Assert.True(p.Active));
        Assert.All(plans, p => Assert.Equal(SubscriptionRules.YearlyPrice(p.MonthlyPrice), p.YearlyPrice));
        using var write = await client.PostAsync("/api/public/plans", null);
        Assert.False(write.IsSuccessStatusCode);
    }

    [Fact]
    public async Task YearlyRequestsChargeTenMonthsForTwelveAndReachTheAdministrator()
    {
        var owner = await Owner(); using var oc = owner.Client;
        var admin = await Owner("admin-yearly@test.example"); using var ac = admin.Client;
        var enterprise = (await Read<PlanDto[]>(await Platform(admin, "/plans"))).Single(p => p.Id == SubscriptionRules.EnterpriseId);
        var priced = await Read<PlanDto>(await Send(admin.Client, $"/api/platform/plans/{enterprise.Id}", HttpMethod.Put,
            new PlanUpdateRequest(enterprise.Name, 12000, enterprise.MaxBranches, enterprise.MaxUsers, enterprise.MaxAdmins, true, enterprise.Version)));
        Assert.Equal(120000, priced.YearlyPrice); Assert.Equal(24000, priced.YearlySaving);

        Assert.Equal(HttpStatusCode.BadRequest, (await Workspace(owner, "/subscription/request", new PlanRequest(SubscriptionRules.EnterpriseId, "Weekly"))).StatusCode);
        var payment = await Read<PaymentStartDto>(await Workspace(owner, "/subscription/request", new PlanRequest(SubscriptionRules.EnterpriseId, SubscriptionRules.Yearly)));
        Assert.Contains("billed yearly", payment.Instructions); Assert.Contains("PKR 120,000.00 for 12 months", payment.Instructions);
        var subscription = await Read<SubscriptionDto>(await Workspace(owner, "/subscription"));
        Assert.Equal(SubscriptionRules.Yearly, subscription.RequestedBillingPeriod);
        Assert.Equal(120000, subscription.Plans.Single(p => p.Id == SubscriptionRules.EnterpriseId).YearlyPrice);
        Assert.Contains(subscription.History, h => h.Kind == "PlanRequested" && h.Note.Contains("Yearly billing"));

        var summary = (await Read<PageDto<TenantSummaryDto>>(await Platform(admin, "/tenants?search=Inventory"))).Items.First(t => t.TenantId == owner.Me.TenantId);
        Assert.Equal(SubscriptionRules.Yearly, summary.RequestedBillingPeriod);
        var active = await Read<TenantSummaryDto>(await Platform(admin, $"/tenants/{owner.Me.TenantId}/activate", new ActivationRequest(SubscriptionRules.EnterpriseId, 12, 120000, "Bank ref yearly")));
        Assert.Null(active.RequestedBillingPeriod); Assert.True(active.PaidThrough > DateTimeOffset.UtcNow.AddMonths(11));
        Assert.Null((await Read<SubscriptionDto>(await Workspace(owner, "/subscription"))).RequestedBillingPeriod);
    }

    [Fact]
    public async Task SupportViewsNeedAnOwnerGrantAreReadOnlyLeastPrivilegeAuditedAndEndOnRevoke()
    {
        var (owner, branch, _) = await ReportingTenant(); using var oc = owner.Client;
        var admin = await Owner("admin-support@test.example"); using var ac = admin.Client;
        Assert.Equal(HttpStatusCode.BadRequest, (await Workspace(owner, "/support", new SupportGrantRequest(73, false, "Too long"))).StatusCode);
        var grant = await Read<SupportGrantDto>(await Workspace(owner, "/support", new SupportGrantRequest(2, false, "Stock count question")));
        Assert.Contains((await Read<SupportGrantDto[]>(await Platform(admin, "/support-grants"))), g => g.Id == grant.Id && g.TenantName == "Inventory tenant");
        (await Platform(admin, $"/support/{grant.Id}/enter", new { })).EnsureSuccessStatusCode();

        var me = await Read<MeDto>(await admin.Client.GetAsync("/api/auth/me"));
        Assert.Equal(owner.Me.TenantId, me.TenantId); Assert.NotNull(me.Support); Assert.False(me.Support.IncludeFinance); Assert.False(me.IsOwner);
        Assert.DoesNotContain(Permissions.FinanceView, me.Permissions); Assert.DoesNotContain(Permissions.SalesPost, me.Permissions);
        Assert.Equal(HttpStatusCode.OK, (await Workspace(admin, "/sales/customers?page=1", tenant: owner.Me.TenantId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Workspace(admin, $"/finance/accounts?branchId={branch}", tenant: owner.Me.TenantId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Workspace(admin, "/sales/customers", new CustomerRequest("Support edit", "", ""), tenant: owner.Me.TenantId)).StatusCode);
        await using var db = database.CreateContext();
        Assert.True(await db.AuditEvents.IgnoreQueryFilters().CountAsync(x => x.TenantId == owner.Me.TenantId && x.Action == "support.viewed" && x.SubjectId == grant.Id) >= 2);
        Assert.Contains("support.entered", await db.AuditEvents.IgnoreQueryFilters().Where(x => x.TenantId == owner.Me.TenantId).Select(x => x.Action).ToArrayAsync());

        (await Workspace(owner, $"/support/{grant.Id}/revoke", new { })).EnsureSuccessStatusCode();
        me = await Read<MeDto>(await admin.Client.GetAsync("/api/auth/me"));
        Assert.Equal(admin.Me.TenantId, me.TenantId); Assert.Null(me.Support);
        Assert.Equal(HttpStatusCode.NotFound, (await Platform(admin, $"/support/{grant.Id}/enter", new { })).StatusCode);

        var withFinance = await Read<SupportGrantDto>(await Workspace(owner, "/support", new SupportGrantRequest(1, true, "Cash question")));
        (await Platform(admin, $"/support/{withFinance.Id}/enter", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await Workspace(admin, $"/finance/accounts?branchId={branch}", tenant: owner.Me.TenantId)).StatusCode);
        (await Platform(admin, "/support/exit", new { })).EnsureSuccessStatusCode();
        Assert.Equal(admin.Me.TenantId, (await Read<MeDto>(await admin.Client.GetAsync("/api/auth/me"))).TenantId);
        var stranger = await Owner(); using var sc = stranger.Client;
        Assert.Equal(HttpStatusCode.Forbidden, (await Platform(stranger, $"/support/{withFinance.Id}/enter", new { })).StatusCode);
    }

    [Fact]
    public async Task OnboardingCompletesFromRealDataAndUsageIsMeteredMonthly()
    {
        var o = await Owner(); using var client = o.Client;
        var onboarding = await Read<OnboardingDto>(await Workspace(o, "/onboarding"));
        Assert.False(onboarding.Dismissed); Assert.All(onboarding.Steps, s => Assert.False(s.Done));
        await Setup(o);
        onboarding = await Read<OnboardingDto>(await Workspace(o, "/onboarding"));
        Assert.True(onboarding.Steps.Single(s => s.Key == "warehouse").Done); Assert.True(onboarding.Steps.Single(s => s.Key == "items").Done);
        (await Workspace(o, "/onboarding", new OnboardingRequest(true), HttpMethod.Put)).EnsureSuccessStatusCode();
        Assert.True((await Read<OnboardingDto>(await Workspace(o, "/onboarding"))).Dismissed);

        var (r, _, _) = await ReportingTenant(); using var rc = r.Client;
        var usage = (await Read<SubscriptionDto>(await Workspace(r, "/subscription"))).Usage;
        Assert.Equal(3, usage.SalesInvoices); Assert.Equal(1, usage.PurchaseInvoices); Assert.Equal(1, usage.Branches); Assert.Equal(1, usage.Users);
        Assert.True(usage.StockDocuments >= 5); Assert.True(usage.FinanceEntries >= 5);
    }
}

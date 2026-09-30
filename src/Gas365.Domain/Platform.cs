namespace Gas365.Domain;

// Platform-wide plan catalogue; prices and limits are edited by the platform administrator.
public sealed class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal? MonthlyPrice { get; set; }
    public int MaxBranches { get; set; }
    public int MaxUsers { get; set; }
    public int MaxAdmins { get; set; }
    public bool Active { get; set; } = true;
    public int SortOrder { get; set; }
    public uint Version { get; set; }
}

// Append-only billing history visible to the business owner and the platform administrator.
public sealed class SubscriptionEvent : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Kind { get; set; } = "";
    public Guid? PlanId { get; set; }
    public decimal? Amount { get; set; }
    public string Reference { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset? PeriodEnd { get; set; }
    public Guid? ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

// Owner-approved, time-limited, read-only support access for the platform team.
public sealed class SupportGrant : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid GrantedBy { get; set; }
    public bool IncludeFinance { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
}

public static class SubscriptionRules
{
    public const string Trial = "Trial";
    public const string Active = "Active";
    public const string Expired = "Expired";
    // Owner-approved on 2026-09-29: new businesses get a 14-day trial of the Business plan.
    public const int TrialDays = 14;
    public const string TrialPlan = "business";
    public const string Monthly = "Monthly";
    public const string Yearly = "Yearly";
    // Owner-approved on 2026-09-30: yearly billing charges ten months for twelve (two months free).
    public const int YearlyMonthsCharged = 10;
    public static decimal? YearlyPrice(decimal? monthlyPrice) => monthlyPrice * YearlyMonthsCharged;
    public static decimal? YearlySaving(decimal? monthlyPrice) => monthlyPrice * (12 - YearlyMonthsCharged);
    public static int PeriodMonths(string period) => period == Yearly ? 12 : 1;
    public static string Period(string? value) => value switch
    {
        null or "" or Monthly => Monthly,
        Yearly => Yearly,
        _ => throw new ArgumentException("Choose monthly or yearly billing.")
    };

    public static readonly Guid StarterId = new("7a1f0c2e-0000-4000-8000-000000000001");
    public static readonly Guid BusinessId = new("7a1f0c2e-0000-4000-8000-000000000002");
    public static readonly Guid EnterpriseId = new("7a1f0c2e-0000-4000-8000-000000000003");

    // Expiry is evaluated when read, so no scheduled job is needed to lock an ended trial or paid period.
    public static string Effective(string status, DateTimeOffset? trialEndsAt, DateTimeOffset? paidThrough, DateTimeOffset now) => status switch
    {
        Trial => trialEndsAt > now ? Trial : Expired,
        Active => paidThrough is null || paidThrough > now ? Active : Expired,
        _ => Expired
    };

    // An expired business stays readable. It may still export its data, choose a plan and grant support access.
    public static bool AllowedWhenExpired(string path) =>
        path.StartsWith("/api/workspace/subscription", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/workspace/support", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/workspace/reports/exports", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/workspace/notifications", StringComparison.OrdinalIgnoreCase);

    // Least privilege: operational read access, with financial views only when the owner includes them.
    // Branch management grants branch visibility only; support sessions can never write.
    public static string[] SupportPermissions(bool includeFinance) => includeFinance
        ? [Permissions.BranchManage, Permissions.InventoryView, Permissions.CustomerView, Permissions.SalesView, Permissions.AuditView,
           Permissions.PurchaseView, Permissions.FinanceView, Permissions.ReportsView, Permissions.SalesProfit]
        : [Permissions.BranchManage, Permissions.InventoryView, Permissions.CustomerView, Permissions.SalesView, Permissions.AuditView];
}

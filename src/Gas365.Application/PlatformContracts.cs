namespace Gas365.Application;

public sealed record PlanDto(Guid Id, string Code, string Name, decimal? MonthlyPrice, int MaxBranches, int MaxUsers, int MaxAdmins, bool Active, uint Version);
public sealed record PlanUpdateRequest(string Name, decimal? MonthlyPrice, int MaxBranches, int MaxUsers, int MaxAdmins, bool Active, uint Version);
public sealed record UsageDto(string Month, int Branches, int Users, int Admins, int SalesInvoices, int PurchaseInvoices, int FinanceEntries, int StockDocuments,
    int Exports, int ImportedRows, long StorageBytes);
public sealed record SubscriptionEventDto(Guid Id, string Kind, string? PlanName, decimal? Amount, string Reference, string Note, DateTimeOffset? PeriodEnd, DateTimeOffset OccurredAt);
public sealed record SubscriptionDto(PlanDto? Plan, string Status, DateTimeOffset? TrialEndsAt, DateTimeOffset? PaidThrough, PlanDto? RequestedPlan, UsageDto Usage,
    PlanDto[] Plans, SubscriptionEventDto[] History, string PaymentProvider);
public sealed record PlanRequest(Guid PlanId);
public sealed record PaymentStartDto(string Provider, string Instructions);
public sealed record SupportGrantRequest(int Hours, bool IncludeFinance, string Reason);
public sealed record SupportGrantDto(Guid Id, Guid TenantId, string TenantName, bool IncludeFinance, string Reason, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, bool Active);
public sealed record OnboardingStepDto(string Key, string Label, string Page, bool Done);
public sealed record OnboardingDto(bool Dismissed, OnboardingStepDto[] Steps);
public sealed record OnboardingRequest(bool Dismissed);

public interface ISubscriptionAccess
{
    Task<SubscriptionDto> SubscriptionAsync(CancellationToken ct);
    Task<PaymentStartDto> RequestPlanAsync(PlanRequest request, CancellationToken ct);
    Task<SupportGrantDto[]> SupportGrantsAsync(CancellationToken ct);
    Task<SupportGrantDto> GrantSupportAsync(SupportGrantRequest request, CancellationToken ct);
    Task RevokeSupportAsync(Guid id, CancellationToken ct);
    Task<OnboardingDto> OnboardingAsync(CancellationToken ct);
    Task SetOnboardingAsync(OnboardingRequest request, CancellationToken ct);
    // Rejects writes with 402 when the trial or paid period has ended, except the documented allow-list.
    Task EnsureWritableAsync(string path, CancellationToken ct);
    Task RecordSupportViewAsync(CancellationToken ct);
}

public sealed record TenantSummaryDto(Guid TenantId, string Name, string? Plan, string Status, DateTimeOffset? TrialEndsAt, DateTimeOffset? PaidThrough, string? RequestedPlan,
    int Users, int Branches, int TransactionsThisMonth, DateTimeOffset CreatedAt);
public sealed record ActivationRequest(Guid PlanId, int Months, decimal Amount, string Reference);
public sealed record TrialExtensionRequest(int Days, string Reason);

// Platform administration never returns tenant financial amounts; support views go through owner grants.
public interface IPlatformAccess
{
    Task<PageDto<TenantSummaryDto>> TenantsAsync(string? search, int page, CancellationToken ct);
    Task<PlanDto[]> PlansAsync(CancellationToken ct);
    Task<PlanDto> UpdatePlanAsync(Guid id, PlanUpdateRequest request, CancellationToken ct);
    Task<TenantSummaryDto> ActivateAsync(Guid tenantId, ActivationRequest request, CancellationToken ct);
    Task<TenantSummaryDto> ExtendTrialAsync(Guid tenantId, TrialExtensionRequest request, CancellationToken ct);
    Task<SupportGrantDto[]> SupportGrantsAsync(CancellationToken ct);
    Task EnterSupportAsync(Guid grantId, CancellationToken ct);
    Task ExitSupportAsync(CancellationToken ct);
}

// Payment integration seam. The first provider records manual (bank transfer) payments confirmed by the platform team.
public interface IPaymentProvider
{
    string Name { get; }
    PaymentStartDto Start(string businessName, PlanDto plan);
}

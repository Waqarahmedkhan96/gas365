using Gas365.Application;

namespace Gas365.Api;

public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this WebApplication app)
    {
        var workspace = app.MapGroup("/api/workspace").WithTags("Subscription, onboarding and support").RequireAuthorization();
        workspace.MapGet("/subscription", (ISubscriptionAccess s, CancellationToken ct) => s.SubscriptionAsync(ct));
        workspace.MapPost("/subscription/request", (PlanRequest r, ISubscriptionAccess s, CancellationToken ct) => s.RequestPlanAsync(r, ct));
        workspace.MapGet("/support", (ISubscriptionAccess s, CancellationToken ct) => s.SupportGrantsAsync(ct));
        workspace.MapPost("/support", (SupportGrantRequest r, ISubscriptionAccess s, CancellationToken ct) => s.GrantSupportAsync(r, ct));
        workspace.MapPost("/support/{id:guid}/revoke", async (Guid id, ISubscriptionAccess s, CancellationToken ct) => { await s.RevokeSupportAsync(id, ct); return Results.NoContent(); });
        workspace.MapGet("/onboarding", (ISubscriptionAccess s, CancellationToken ct) => s.OnboardingAsync(ct));
        workspace.MapPut("/onboarding", async (OnboardingRequest r, ISubscriptionAccess s, CancellationToken ct) => { await s.SetOnboardingAsync(r, ct); return Results.NoContent(); });

        // Platform routes carry no X-Workspace header; the service authorizes the platform administrator itself.
        var platform = app.MapGroup("/api/platform").WithTags("Platform administration").RequireAuthorization();
        platform.MapGet("/tenants", (IPlatformAccess s, CancellationToken ct, string? search = null, int page = 1) => s.TenantsAsync(search, page, ct));
        platform.MapGet("/plans", (IPlatformAccess s, CancellationToken ct) => s.PlansAsync(ct));
        platform.MapPut("/plans/{id:guid}", (Guid id, PlanUpdateRequest r, IPlatformAccess s, CancellationToken ct) => s.UpdatePlanAsync(id, r, ct));
        platform.MapPost("/tenants/{id:guid}/activate", (Guid id, ActivationRequest r, IPlatformAccess s, CancellationToken ct) => s.ActivateAsync(id, r, ct));
        platform.MapPost("/tenants/{id:guid}/extend-trial", (Guid id, TrialExtensionRequest r, IPlatformAccess s, CancellationToken ct) => s.ExtendTrialAsync(id, r, ct));
        platform.MapGet("/support-grants", (IPlatformAccess s, CancellationToken ct) => s.SupportGrantsAsync(ct));
        platform.MapPost("/support/{grantId:guid}/enter", async (Guid grantId, IPlatformAccess s, CancellationToken ct) => { await s.EnterSupportAsync(grantId, ct); return Results.NoContent(); });
        platform.MapPost("/support/exit", async (IPlatformAccess s, CancellationToken ct) => { await s.ExitSupportAsync(ct); return Results.NoContent(); });
    }
}

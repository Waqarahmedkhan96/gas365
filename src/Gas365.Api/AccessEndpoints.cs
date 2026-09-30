using System.Security.Claims;
using Gas365.Application;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Gas365.Api;

public static class AccessEndpoints
{
    internal static async Task SignInAsync(HttpContext context, SessionGrant grant)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, grant.UserId.ToString()), new Claim("session", grant.SessionId.ToString())
        ], CookieAuthenticationDefaults.AuthenticationScheme));
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = grant.ExpiresAt, AllowRefresh = false });
    }

    public static void MapAccessEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Identity").RequireRateLimiting("auth");
        auth.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
        }).RequireRateLimiting("session");
        auth.MapPost("/register", async (RegisterRequest request, IIdentityAccess service, CancellationToken ct) =>
        { await service.RegisterAsync(request, ct); return Results.NoContent(); });
        auth.MapPost("/login", async (LoginRequest request, IIdentityAccess service, HttpContext context, CancellationToken ct) =>
        { await SignInAsync(context, await service.LoginAsync(request, ct)); return Results.NoContent(); });
        auth.MapPost("/confirm", async (ConfirmRequest request, IIdentityAccess service, CancellationToken ct) =>
        { await service.ConfirmAsync(request, ct); return Results.NoContent(); });
        auth.MapPost("/forgot", async (EmailRequest request, IIdentityAccess service, CancellationToken ct) =>
        { await service.ForgotAsync(request, ct); return Results.NoContent(); });
        auth.MapPost("/resend", async (EmailRequest request, IIdentityAccess service, CancellationToken ct) =>
        { await service.ResendAsync(request, ct); return Results.NoContent(); });
        auth.MapPost("/reset", async (ResetRequest request, IIdentityAccess service, CancellationToken ct) =>
        { await service.ResetAsync(request, ct); return Results.NoContent(); });
        auth.MapGet("/me", (IIdentityAccess service, CancellationToken ct) => service.MeAsync(ct)).RequireAuthorization().RequireRateLimiting("session");
        auth.MapPost("/switch", async (SwitchRequest request, IIdentityAccess service, HttpContext context, CancellationToken ct) =>
        { await SignInAsync(context, await service.SwitchAsync(request.MembershipId, ct)); return Results.NoContent(); }).RequireAuthorization();
        auth.MapExternalSignIn(app.Configuration);
        auth.MapPost("/logout", async (IIdentityAccess service, TenantScope scope, HttpContext context, CancellationToken ct) =>
        { await service.RevokeAsync(scope.SessionId, ct); await context.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
        auth.MapGet("/sessions", (IIdentityAccess service, CancellationToken ct) => service.SessionsAsync(ct)).RequireAuthorization().RequireRateLimiting("session");
        auth.MapDelete("/sessions/{id:guid}", async (Guid id, IIdentityAccess service, CancellationToken ct) =>
        { await service.RevokeAsync(id, ct); return Results.NoContent(); }).RequireAuthorization();

        var workspace = app.MapGroup("/api/workspace").WithTags("Workspace").RequireAuthorization();
        workspace.MapGet("/tenant", (IWorkspaceAccess service, CancellationToken ct) => service.TenantAsync(ct));
        workspace.MapPut("/tenant", async (TenantRequest request, IWorkspaceAccess service, CancellationToken ct) =>
        { await service.UpdateTenantAsync(request, ct); return Results.NoContent(); });
        workspace.MapGet("/branches", (IWorkspaceAccess service, CancellationToken ct) => service.BranchesAsync(ct));
        workspace.MapGet("/branches/{id:guid}", (Guid id, IWorkspaceAccess service, CancellationToken ct) => service.BranchAsync(id, ct));
        workspace.MapPost("/branches", (BranchRequest request, IWorkspaceAccess service, CancellationToken ct) => service.CreateBranchAsync(request, ct));
        workspace.MapPut("/branches/{id:guid}", async (Guid id, BranchRequest request, IWorkspaceAccess service, CancellationToken ct) =>
        { await service.UpdateBranchAsync(id, request, ct); return Results.NoContent(); });
        workspace.MapGet("/roles", (IWorkspaceAccess service, CancellationToken ct) => service.RolesAsync(ct));
        workspace.MapPost("/roles", (RoleRequest request, IWorkspaceAccess service, CancellationToken ct) => service.CreateRoleAsync(request, ct));
        workspace.MapPut("/roles/{id:guid}", async (Guid id, RoleRequest request, IWorkspaceAccess service, CancellationToken ct) =>
        { await service.UpdateRoleAsync(id, request, ct); return Results.NoContent(); });
        workspace.MapGet("/members", (IWorkspaceAccess service, CancellationToken ct) => service.MembersAsync(ct));
        workspace.MapPost("/members", async (InviteRequest request, IWorkspaceAccess service, CancellationToken ct) =>
        { await service.InviteAsync(request, ct); return Results.NoContent(); });
        workspace.MapPut("/members/{id:guid}", async (Guid id, MemberRequest request, IWorkspaceAccess service, CancellationToken ct) =>
        { await service.UpdateMemberAsync(id, request, ct); return Results.NoContent(); });
        workspace.MapGet("/audit", (IWorkspaceAccess service, CancellationToken ct) => service.AuditAsync(ct));
    }
}

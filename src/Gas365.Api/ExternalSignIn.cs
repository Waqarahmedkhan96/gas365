using System.Security.Claims;
using Gas365.Application;
using Microsoft.AspNetCore.Authentication;

namespace Gas365.Api;

// Google sign-in: challenge, callback completion, pending sign-up and completion of a new business.
public static class ExternalSignIn
{
    public const string Scheme = "External";
    public const string EmailVerifiedClaim = "email_verified";
    private const string Provider = "Google";

    public static bool GoogleConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);

    private static ExternalIdentity? Identity(AuthenticateResult result)
    {
        if (!result.Succeeded || result.Principal is null) return null;
        var p = result.Principal;
        return new ExternalIdentity(Provider, p.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", p.FindFirstValue(ClaimTypes.Email) ?? "",
            string.Equals(p.FindFirstValue(EmailVerifiedClaim), "true", StringComparison.OrdinalIgnoreCase), p.FindFirstValue(ClaimTypes.Name) ?? "");
    }

    // A same-site page navigation, so the strict session cookie set here is sent with the next request.
    private static IResult Continue(string path) => Results.Content(
        $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta http-equiv=\"refresh\" content=\"0;url={path}\"><title>Signing in · Gas365</title></head>"
        + $"<body><p>Signing in… <a href=\"{path}\">Continue</a></p></body></html>", "text/html");

    public static void MapExternalSignIn(this RouteGroupBuilder auth, IConfiguration configuration)
    {
        var enabled = GoogleConfigured(configuration);
        auth.MapGet("/providers", () => new { google = enabled }).RequireRateLimiting("session");
        if (!enabled) return;
        auth.MapGet("/google/start", () =>
            Results.Challenge(new AuthenticationProperties { RedirectUri = "/api/auth/google/complete" }, [Provider]));
        auth.MapGet("/google/complete", async (HttpContext context, IIdentityAccess service, CancellationToken ct) =>
        {
            var identity = Identity(await context.AuthenticateAsync(Scheme));
            if (identity is null) return Continue("/login?error=google");
            try
            {
                var grant = await service.ExternalLoginAsync(identity, ct);
                if (grant is null) return Continue("/register/google");
                await context.SignOutAsync(Scheme);
                await AccessEndpoints.SignInAsync(context, grant);
                return Continue("/workspace");
            }
            catch (AccessException)
            {
                await context.SignOutAsync(Scheme);
                return Continue("/login?error=google");
            }
        });
        auth.MapGet("/google/pending", async (HttpContext context) =>
            Identity(await context.AuthenticateAsync(Scheme)) is { } identity
                ? Results.Ok(new ExternalPendingDto(identity.Email, identity.Name))
                : Results.Unauthorized());
        auth.MapPost("/google/register", async (ExternalRegisterRequest request, HttpContext context, IIdentityAccess service, CancellationToken ct) =>
        {
            var identity = Identity(await context.AuthenticateAsync(Scheme));
            if (identity is null) return Results.Problem(statusCode: 401, title: "Your Google sign-in expired. Start again.");
            var grant = await service.ExternalRegisterAsync(identity, request, ct);
            await context.SignOutAsync(Scheme);
            await AccessEndpoints.SignInAsync(context, grant);
            return Results.NoContent();
        });
    }
}

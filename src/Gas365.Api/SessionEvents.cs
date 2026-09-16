using System.Security.Claims;
using Gas365.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Gas365.Api;

public sealed class SessionEvents(IIdentityAccess identity) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue("session"), out var session) ||
            !Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var user) ||
            !await identity.ValidateSessionAsync(session, user, context.HttpContext.RequestAborted))
            context.RejectPrincipal();
    }
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    { context.Response.StatusCode = 401; return Task.CompletedTask; }
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    { context.Response.StatusCode = 403; return Task.CompletedTask; }
}

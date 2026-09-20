using System.Threading.RateLimiting;
using Gas365.Application;
using Gas365.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gas365.Api;

public static class AccessSetup
{
    public static void AddAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<TenantScope>();
        builder.Services.AddScoped<IIdentityAccess, IdentityAccess>();
        builder.Services.AddScoped<IWorkspaceAccess, WorkspaceAccess>();
        builder.Services.AddScoped<ICatalogAccess, CatalogAccess>();
        builder.Services.AddScoped<IInventoryAccess, InventoryAccess>();
        builder.Services.AddScoped<IPurchasingAccess, PurchasingAccess>();
        builder.Services.AddSingleton<Gas365.Domain.IInventoryCosting, Gas365.Domain.WeightedAverageCosting>();
        builder.Services.AddScoped<IAccountMessages, AccountMessages>();
        builder.Services.AddScoped<SessionEvents>();
        builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedEmail = true;
        }).AddEntityFrameworkStores<Gas365DbContext>().AddDefaultTokenProviders();
        builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(1));
        var development = builder.Environment.IsDevelopment();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = development ? "Gas365.Session" : "__Host-Gas365.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.EventsType = typeof(SessionEvents);
        });
        builder.Services.AddAuthorization();
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        var protection = builder.Services.AddDataProtection().SetApplicationName("Gas365");
        if (builder.Configuration["DataProtection:KeyPath"] is string keyPath)
            protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }

    public static void UseAccess(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try { await next(context); }
            catch (AccessException exception) { await Results.Problem(statusCode: exception.StatusCode, title: exception.Message).ExecuteAsync(context); }
            catch (DbUpdateConcurrencyException) { await Results.Problem(statusCode: 409, title: "The record changed. Refresh and try again.").ExecuteAsync(context); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" or "23503" or "23514" })
            { await Results.Problem(statusCode: 409, title: "This change conflicts with an existing record or limit.").ExecuteAsync(context); }
            catch (System.Net.Mail.SmtpException) { await Results.Problem(statusCode: 503, title: "Account email delivery is unavailable. Try again later.").ExecuteAsync(context); }
        });
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.Headers.CacheControl = "no-store";
                if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
                {
                    try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
                    catch (AntiforgeryValidationException)
                    { await Results.Problem(statusCode: 400, title: "Refresh the page and try again.").ExecuteAsync(context); return; }
                }
                if (context.Request.Path.StartsWithSegments("/api/workspace"))
                {
                    var scope = context.RequestServices.GetRequiredService<TenantScope>();
                    if (!Guid.TryParse(context.Request.Headers["X-Workspace"], out var expected) || expected != scope.TenantId)
                    { await Results.Problem(statusCode: 409, title: "Your active workspace changed. Refresh the page.").ExecuteAsync(context); return; }
                }
            }
            await next(context);
        });
        app.MapAccessEndpoints();
        app.MapInventoryEndpoints();
        app.MapPurchasingEndpoints();
    }
}

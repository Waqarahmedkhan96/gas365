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
        builder.Services.AddScoped<ICustomerAccess, CustomerAccess>();
        builder.Services.AddScoped<ISalesDraftAccess, SalesDraftAccess>();
        builder.Services.AddScoped<ISalesAccess, SalesAccess>();
        builder.Services.AddScoped<ICylinderLoanAccess, CylinderLoanAccess>();
        builder.Services.AddScoped<IFinanceAccess, FinanceAccess>();
        builder.Services.AddScoped<IReportAccess, ReportAccess>();
        builder.Services.AddScoped<IExportProcessor, ExportProcessor>();
        builder.Services.AddScoped<INotificationAccess, NotificationAccess>();
        builder.Services.AddScoped<INotificationSweeper, NotificationSweeper>();
        builder.Services.AddScoped<IImportAccess, ImportAccess>();
        builder.Services.AddScoped<IAuditAccess, AuditAccess>();
        if (builder.Configuration.GetValue("Exports:WorkerEnabled", true)) builder.Services.AddHostedService<ExportWorker>();
        if (builder.Configuration.GetValue("Notifications:WorkerEnabled", true)) builder.Services.AddHostedService<NotificationWorker>();
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
            // Read-only session checks run on every page load; shops and mobile carriers often share one public IP.
            // Keep them outside the strict credential budget and count them per signed-in user where possible.
            options.AddPolicy("session", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }

    // Cash-register guards in FinanceAndRegisters.Guards; other constraint messages stay generic.
    private static readonly HashSet<string> RegisterGuardMessages = ["Cash register is not open", "Closed branch day requires reopening", "Closed business day requires reopening"];

    private static bool IsTransactionConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException { SqlState: "40001" or "40P01" }) return true;
        return false;
    }

    public static void UseAccess(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try { await next(context); }
            catch (AccessException exception) { await Results.Problem(statusCode: exception.StatusCode, title: exception.Message).ExecuteAsync(context); }
            catch (DbUpdateConcurrencyException) { await Results.Problem(statusCode: 409, title: "The record changed. Refresh and try again.").ExecuteAsync(context); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23514" } guard && RegisterGuardMessages.Contains(guard.MessageText))
            { await Results.Problem(statusCode: 409, title: guard.MessageText + ". Ask an authorized user to open or reopen the cash register.").ExecuteAsync(context); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" or "23503" or "23514" or "40001" or "40P01" })
            { await Results.Problem(statusCode: 409, title: "This change conflicts with an existing record or limit.").ExecuteAsync(context); }
            catch (Exception exception) when (IsTransactionConflict(exception))
            { await Results.Problem(statusCode: 409, title: "A concurrent change occurred. Refresh before trying again.").ExecuteAsync(context); }
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
        app.MapCustomerEndpoints();
        app.MapFinanceEndpoints();
        app.MapReportEndpoints();
        app.MapOperationsEndpoints();
    }
}

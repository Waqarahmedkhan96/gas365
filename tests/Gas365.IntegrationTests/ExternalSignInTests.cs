using System.Net;
using Gas365.Application;
using Gas365.Domain;
using Gas365.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gas365.IntegrationTests;

public sealed partial class CustomerTests
{
    private WebApplicationFactory<Program> WithGoogle() => app.WithWebHostBuilder(builder =>
    {
        // Host settings are visible while services are registered, before the configuration callbacks run.
        builder.UseSetting("Authentication:Google:ClientId", "test-client.apps.googleusercontent.com");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-secret");
    });

    private static async Task<T> Scoped<T>(WebApplicationFactory<Program> factory, Func<IIdentityAccess, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IIdentityAccess>());
    }

    [Fact]
    public async Task GoogleSignInStaysOffUntilBothKeysAreConfigured()
    {
        using var client = Client();
        var providers = await client.GetStringAsync("/api/auth/providers");
        Assert.Contains("\"google\":false", providers);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/auth/google/start")).StatusCode);

        await using var google = WithGoogle();
        using var enabled = google.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        Assert.Contains("\"google\":true", await enabled.GetStringAsync("/api/auth/providers"));
        using var start = await enabled.GetAsync("/api/auth/google/start");
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var location = start.Headers.Location!.ToString();
        Assert.StartsWith("https://accounts.google.com/", location);
        Assert.Contains(Uri.EscapeDataString("https://localhost/api/auth/google/callback"), location);
        Assert.Equal(HttpStatusCode.Unauthorized, (await enabled.GetAsync("/api/auth/google/pending")).StatusCode);
    }

    [Fact]
    public async Task VerifiedGoogleIdentitiesCreateOrLinkAccountsAndUnverifiedOnesAreRefused()
    {
        var email = $"google-{Guid.NewGuid():N}@test.example";
        var identity = new ExternalIdentity("Google", $"sub-{Guid.NewGuid():N}", email, true, "Google Owner");

        Assert.Null(await Scoped<SessionGrant?>(app, s => s.ExternalLoginAsync(identity, default)));
        var unverified = await Assert.ThrowsAsync<AccessException>(() => Scoped(app, s => s.ExternalLoginAsync(identity with { EmailVerified = false }, default)));
        Assert.Equal(401, unverified.StatusCode);

        var created = await Scoped(app, s => s.ExternalRegisterAsync(identity, new ExternalRegisterRequest("Google Owner", "Google Gas"), default));
        var again = await Scoped<SessionGrant?>(app, s => s.ExternalLoginAsync(identity, default));
        Assert.NotNull(again); Assert.Equal(created.UserId, again!.UserId);
        var duplicate = await Assert.ThrowsAsync<AccessException>(() => Scoped(app, s => s.ExternalRegisterAsync(identity, new ExternalRegisterRequest("Again", "Again"), default)));
        Assert.Equal(409, duplicate.StatusCode);

        await using (var db = database.CreateContext())
        {
            var user = await db.Users.SingleAsync(u => u.Id == created.UserId);
            Assert.True(user.EmailConfirmed); Assert.Null(user.PasswordHash);
            var tenant = await db.Memberships.IgnoreQueryFilters().Where(m => m.UserId == created.UserId)
                .Join(db.Tenants.IgnoreQueryFilters(), m => m.TenantId, t => t.TenantId, (m, t) => t).SingleAsync();
            Assert.Equal("Google Gas", tenant.Name); Assert.Equal(SubscriptionRules.Trial, tenant.SubscriptionStatus);
            Assert.Contains(await db.AuditEvents.IgnoreQueryFilters().Where(a => a.TenantId == tenant.TenantId).Select(a => a.Action).ToArrayAsync(), a => a == "login.external");
        }

        // An existing password account is linked when Google verifies the same address.
        var owner = await Owner(); using var oc = owner.Client;
        var linked = await Scoped<SessionGrant?>(app, s => s.ExternalLoginAsync(new ExternalIdentity("Google", $"sub-{Guid.NewGuid():N}", owner.Me.Email, true, "Owner"), default));
        Assert.NotNull(linked); Assert.Equal(owner.Me.UserId, linked!.UserId);
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Single(await users.GetLoginsAsync((await users.FindByIdAsync(owner.Me.UserId.ToString()))!));
    }
}

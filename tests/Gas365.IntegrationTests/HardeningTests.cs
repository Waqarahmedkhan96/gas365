using System.Net;
using Gas365.Application;
using Gas365.Domain;
using Gas365.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gas365.IntegrationTests;

public sealed partial class CustomerTests
{
    [Fact]
    public async Task RetentionRemovesOnlyOldOperationalDataAndNeverLedgers()
    {
        var (o, branch, _) = await ReportingTenant(); using var client = o.Client; var tenant = o.Me.TenantId;
        await using var db = database.CreateContext();
        var ledgerBefore = await db.SaleEntries.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenant);
        var auditBefore = await db.AuditEvents.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenant);
        var oldNote = Guid.NewGuid(); var freshNote = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Notifications" ("TenantId","Id","Kind","Permission","SubjectId","Key","Title","Body","CreatedAt","ResolvedAt")
            VALUES ({tenant},{oldNote},'LowStock','inventory.view',{Guid.NewGuid()},{"old:" + oldNote},'Old','Old',now() - interval '300 days',now() - interval '200 days'),
                   ({tenant},{freshNote},'LowStock','inventory.view',{Guid.NewGuid()},{"fresh:" + freshNote},'Fresh','Fresh',now() - interval '20 days',now() - interval '10 days')
            """);
        var membership = await db.Memberships.IgnoreQueryFilters().FirstAsync(x => x.TenantId == tenant);
        var oldSession = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "LoginSessions" ("TenantId","Id","MembershipId","CreatedAt","ExpiresAt","LastSeenAt","Revoked","SecurityStamp")
            VALUES ({tenant},{oldSession},{membership.Id},now() - interval '60 days',now() - interval '59 days',now() - interval '59 days',TRUE,'x')
            """);
        using (var scope = app.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<RetentionService>().RunAsync(default);
            Assert.True(result.Notifications >= 1); Assert.True(result.Sessions >= 1);
        }
        Assert.False(await db.Notifications.IgnoreQueryFilters().AnyAsync(x => x.Id == oldNote));
        Assert.True(await db.Notifications.IgnoreQueryFilters().AnyAsync(x => x.Id == freshNote));
        Assert.False(await db.LoginSessions.IgnoreQueryFilters().AnyAsync(x => x.Id == oldSession));
        Assert.Equal(ledgerBefore, await db.SaleEntries.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenant));
        Assert.Equal(auditBefore, await db.AuditEvents.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenant));
        Assert.Equal(HttpStatusCode.OK, (await Sales(o, "customers?page=1")).StatusCode); // The owner's live session is untouched.
    }

    [Fact]
    public async Task FailingBackgroundJobsDegradeReadinessWithoutTakingTheApiDown()
    {
        await using var db = database.CreateContext();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "JobStates" ("Name","LastStartedAt","LastFailedAt","LastError","ConsecutiveFailures") VALUES ('probe-job', now(), now(), 'Probe failure', 3)
            ON CONFLICT ("Name") DO UPDATE SET "ConsecutiveFailures" = 3
            """);
        using var client = Client();
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode); Assert.Contains("Degraded", await ready.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"JobStates\" WHERE \"Name\" = 'probe-job'");
    }
}

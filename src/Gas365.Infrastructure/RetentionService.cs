using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed record RetentionResult(int Notifications, int Sessions, int ImportRows);

// Removes only operational leftovers. Financial ledgers, audit events, billing history and import/export job records
// are never deleted here; export file contents are already cleared at expiry by the export worker.
public sealed class RetentionService(Gas365DbContext db)
{
    public static readonly TimeSpan ResolvedNotifications = TimeSpan.FromDays(180);
    public static readonly TimeSpan EndedSessions = TimeSpan.FromDays(30);
    public static readonly TimeSpan CommittedImportRows = TimeSpan.FromDays(90);

    public async Task<RetentionResult> RunAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var notifications = await db.Notifications.IgnoreQueryFilters().Where(n => n.ResolvedAt < now - ResolvedNotifications).ExecuteDeleteAsync(ct);
        var sessions = await db.LoginSessions.IgnoreQueryFilters().Where(s => (s.Revoked || s.ExpiresAt < now) && s.ExpiresAt < now - EndedSessions).ExecuteDeleteAsync(ct);
        // Rows of fully committed imports older than 90 days; the batch record with its counts remains as history.
        var rows = await db.ImportRows.IgnoreQueryFilters().Where(r => db.ImportBatches.IgnoreQueryFilters()
            .Any(b => b.TenantId == r.TenantId && b.Id == r.BatchId && b.Status == Domain.ImportStatus.Committed && b.CommittedAt < now - CommittedImportRows)).ExecuteDeleteAsync(ct);
        return new(notifications, sessions, rows);
    }
}

using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gas365.Infrastructure;

// Runs in a fresh DI scope per job. The requester's tenant scope and live permissions are rebuilt from the
// database, so revoked access stops queued exports instead of trusting the permissions held at request time.
public sealed class ExportProcessor(Gas365DbContext db, TenantScope scope, ILogger<ExportProcessor> logger) : IExportProcessor
{
    private sealed class Claim { public Guid TenantId { get; set; } public Guid Id { get; set; } public int Attempts { get; set; } }

    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        // Jobs whose lease ran out after the final attempt are failed rather than retried forever.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ExportJobs" SET "Status" = 'Failed', "Error" = 'Export failed after repeated attempts.', "LockedUntil" = NULL
            WHERE "Status" = 'Running' AND "LockedUntil" < now() AND "Attempts" >= {ExportStatus.MaxAttempts}
            """, ct);
        var lease = ExportStatus.Lease.TotalSeconds;
        var claim = (await db.Database.SqlQuery<Claim>($"""
            UPDATE "ExportJobs" SET "Status" = 'Running', "Attempts" = "Attempts" + 1, "StartedAt" = now(), "LockedUntil" = now() + make_interval(secs => {lease})
            WHERE ("TenantId", "Id") = (
                SELECT "TenantId", "Id" FROM "ExportJobs"
                WHERE ("Status" = 'Queued' OR "Status" = 'Running' AND "LockedUntil" < now()) AND "Attempts" < {ExportStatus.MaxAttempts}
                ORDER BY "RequestedAt" LIMIT 1 FOR UPDATE SKIP LOCKED)
            RETURNING "TenantId", "Id", "Attempts"
            """).ToListAsync(ct)).SingleOrDefault();
        if (claim is null) return false;
        scope.TenantId = claim.TenantId;
        var job = await db.ExportJobs.AsNoTracking().SingleAsync(x => x.Id == claim.Id, ct);
        try
        {
            await LoadRequesterAsync(job, ct);
            var filter = JsonSerializer.Deserialize<ReportFilter>(job.Filter)!;
            var data = await new ReportAccess(db, scope).BuildAsync(job.ReportKey, filter, ct);
            if (job.Format == "pdf" && data.Rows.Count > ReportFiles.MaxPdfRows)
                throw new AccessException(413, $"PDF exports are limited to {ReportFiles.MaxPdfRows:N0} rows. Use CSV or XLSX.");
            var now = DateTimeOffset.UtcNow; var content = ReportFiles.Render(data, job.Format, now);
            await db.ExportJobs.Where(x => x.Id == job.Id).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, ExportStatus.Completed).SetProperty(x => x.Content, content).SetProperty(x => x.ContentType, ReportFiles.ContentType(job.Format))
                .SetProperty(x => x.RowCount, data.Rows.Count).SetProperty(x => x.CompletedAt, now).SetProperty(x => x.ExpiresAt, now + ExportStatus.Lifetime)
                .SetProperty(x => x.LockedUntil, (DateTimeOffset?)null).SetProperty(x => x.Error, ""), ct);
            db.AuditEvents.Add(new AuditEvent { TenantId = job.TenantId, ActorId = job.UserId, Action = "report.export_completed", SubjectId = job.Id });
            await db.SaveChangesAsync(ct);
        }
        catch (AccessException exception)
        {
            // Permission, validation and size problems will not succeed on retry.
            await FinishAsync(job.Id, ExportStatus.Failed, exception.Message, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Export {ExportId} attempt {Attempt} failed.", job.Id, claim.Attempts);
            var final = claim.Attempts >= ExportStatus.MaxAttempts;
            await FinishAsync(job.Id, final ? ExportStatus.Failed : ExportStatus.Queued, final ? "Export failed after repeated attempts." : "Retrying after a processing error.", ct);
        }
        return true;
    }

    private async Task LoadRequesterAsync(ExportJob job, CancellationToken ct)
    {
        var member = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.Id == job.MembershipId && x.UserId == job.UserId && x.Active, ct)
            ?? throw new AccessException(403, "The requester no longer has access to this workspace.");
        var role = await db.TenantRoles.AsNoTracking().SingleAsync(x => x.Id == member.RoleId, ct);
        scope.MembershipId = member.Id; scope.UserId = member.UserId; scope.IsOwner = role.IsOwner; scope.Permissions = role.Permissions;
        if (!scope.Has(Permissions.ReportsExport)) throw new AccessException(403, "Report export permission was removed.");
    }

    private Task FinishAsync(Guid id, string status, string error, CancellationToken ct) =>
        db.ExportJobs.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status).SetProperty(x => x.Error, error).SetProperty(x => x.LockedUntil, (DateTimeOffset?)null), ct);

    // Expired files are removed; the job row stays as the audit trail of what was exported.
    public Task<int> ExpireAsync(CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE "ExportJobs" SET "Status" = 'Expired', "Content" = NULL WHERE "Status" = 'Completed' AND "ExpiresAt" < now()
        """, ct);
}

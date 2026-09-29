using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class ReportAccess
{
    private const int MaxActiveExports = 5;
    private static readonly string[] Formats = ["csv", "xlsx", "pdf"];

    private static ExportJobDto Dto(ExportJob x) => new(x.Id, x.ReportKey, x.Format, x.Status, x.Attempts, x.Error, x.FileName, x.RowCount, x.RequestedAt, x.CompletedAt, x.ExpiresAt);

    public async Task<ExportJobDto> RequestExportAsync(ExportRequest r, CancellationToken ct)
    {
        Require(Permissions.ReportsExport);
        if (CatalogAccess.Text(r.IdempotencyKey, 80) != r.IdempotencyKey) throw new AccessException(400, "Invalid command key.");
        var hash = Hash(r);
        var prior = await db.ExportJobs.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == r.IdempotencyKey, ct);
        if (prior is not null)
        {
            if (prior.RequestHash != hash || prior.MembershipId != scope.MembershipId) throw new AccessException(409, "Command key already used for a different export.");
            return Dto(prior);
        }
        if (!Formats.Contains(r.Format, StringComparer.Ordinal)) throw new AccessException(400, "Choose CSV, XLSX or PDF.");
        // Validate everything the worker will need now, so bad requests fail fast instead of in the queue.
        var d = Find(r.ReportKey); var filter = Normalize(d, new ReportFilter(r.BranchId, r.From, r.To, r.Type));
        await BranchesAsync(filter.BranchId, ct);
        if (await db.ExportJobs.CountAsync(x => x.MembershipId == scope.MembershipId && (x.Status == ExportStatus.Queued || x.Status == ExportStatus.Running), ct) >= MaxActiveExports)
            throw new AccessException(429, "Wait for your current exports to finish before requesting more.");
        var job = new ExportJob
        {
            TenantId = scope.TenantId,
            MembershipId = scope.MembershipId,
            UserId = scope.UserId,
            ReportKey = d.Key,
            Format = r.Format,
            Filter = JsonSerializer.Serialize(filter),
            FileName = ReportFiles.FileName(d.Key, filter, r.Format),
            IdempotencyKey = r.IdempotencyKey,
            RequestHash = hash
        };
        db.ExportJobs.Add(job); Audit("report.export_requested", job.Id); await db.SaveChangesAsync(ct);
        return Dto(job);
    }

    public async Task<PageDto<ExportJobDto>> ExportsAsync(int page, CancellationToken ct)
    {
        Require(Permissions.ReportsExport);
        if (page is < 1 or > 100_000) throw new AccessException(400, "Invalid page.");
        var query = db.ExportJobs.AsNoTracking().Where(x => x.MembershipId == scope.MembershipId);
        var rows = await query.OrderByDescending(x => x.RequestedAt).Skip((page - 1) * 20).Take(20).ToArrayAsync(ct);
        return new(rows.Select(Dto).ToArray(), await query.CountAsync(ct), page, 20);
    }

    // Downloads are private to the requesting member and recheck the report permission at download time.
    public async Task<ExportFile> DownloadAsync(Guid id, CancellationToken ct)
    {
        Require(Permissions.ReportsExport);
        var job = await db.ExportJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.MembershipId == scope.MembershipId, ct) ?? throw new AccessException(404, "Export not found.");
        Find(job.ReportKey);
        if (job.Status == ExportStatus.Expired || job.Status == ExportStatus.Completed && job.ExpiresAt <= DateTimeOffset.UtcNow) throw new AccessException(410, "This download has expired. Request the export again.");
        if (job.Status != ExportStatus.Completed || job.Content is null) throw new AccessException(409, "The export is not ready yet.");
        Audit("report.export_downloaded", job.Id); await db.SaveChangesAsync(ct);
        return new(job.FileName, job.ContentType, job.Content);
    }
}

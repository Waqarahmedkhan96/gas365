using Gas365.Application;

namespace Gas365.Api;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/workspace/reports").WithTags("Reports and exports").RequireAuthorization();
        g.MapGet("", (IReportAccess s) => s.Definitions());
        g.MapGet("/dashboard", (DateOnly from, DateOnly to, IReportAccess s, CancellationToken ct, Guid? branchId = null) => s.DashboardAsync(branchId, from, to, ct));
        g.MapGet("/exports", (IReportAccess s, CancellationToken ct, int page = 1) => s.ExportsAsync(page, ct));
        g.MapPost("/exports", (ExportRequest r, IReportAccess s, CancellationToken ct) => s.RequestExportAsync(r, ct));
        g.MapGet("/exports/{id:guid}/download", async (Guid id, IReportAccess s, CancellationToken ct) =>
        {
            var file = await s.DownloadAsync(id, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });
        g.MapGet("/{key}", (string key, DateOnly from, DateOnly to, IReportAccess s, CancellationToken ct, Guid? branchId = null, string? type = null, int page = 1) =>
            s.RunAsync(key, new ReportFilter(branchId, from, to, type), page, ct));
    }
}

using Gas365.Application;
using Gas365.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Gas365.Api;

public static class OperationsEndpoints
{
    public static void MapOperationsEndpoints(this WebApplication app)
    {
        var notes = app.MapGroup("/api/workspace/notifications").WithTags("Notifications").RequireAuthorization();
        notes.MapGet("", (INotificationAccess s, CancellationToken ct, int page = 1) => s.PageAsync(page, ct));
        notes.MapGet("/summary", (INotificationAccess s, CancellationToken ct) => s.SummaryAsync(ct));
        notes.MapPost("/{id:guid}/read", async (Guid id, INotificationAccess s, CancellationToken ct) => { await s.MarkReadAsync(id, ct); return Results.NoContent(); });
        notes.MapPost("/read-all", async (INotificationAccess s, CancellationToken ct) => { await s.MarkAllReadAsync(ct); return Results.NoContent(); });

        var imports = app.MapGroup("/api/workspace/imports").WithTags("Imports").RequireAuthorization();
        imports.MapGet("/types", (IImportAccess s) => s.Types());
        imports.MapGet("", (IImportAccess s, CancellationToken ct, int page = 1) => s.BatchesAsync(page, ct));
        // The raw file is the request body; the global 32 KB limit is raised only for this endpoint.
        imports.MapPost("", (string type, string fileName, HttpRequest request, IImportAccess s, CancellationToken ct) => s.UploadAsync(type, fileName, request.Body, ct))
            .WithMetadata(new RequestSizeLimitAttribute(ImportStatus.MaxBytes + 1024));
        imports.MapGet("/{id:guid}", (Guid id, IImportAccess s, CancellationToken ct) => s.BatchAsync(id, ct));
        imports.MapPut("/{id:guid}/mapping", (Guid id, ImportMappingRequest r, IImportAccess s, CancellationToken ct) => s.MapAsync(id, r, ct));
        imports.MapPost("/{id:guid}/commit", (Guid id, ImportCommitRequest r, IImportAccess s, CancellationToken ct) => s.CommitAsync(id, r, ct));
        imports.MapGet("/{id:guid}/errors", async (Guid id, IImportAccess s, CancellationToken ct) =>
        {
            var file = await s.ErrorsAsync(id, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });

        var audit = app.MapGroup("/api/workspace/audit").WithTags("Audit and corrections").RequireAuthorization();
        audit.MapGet("/timeline", (DateOnly from, DateOnly to, IAuditAccess s, CancellationToken ct, string? area = null, Guid? actorId = null, Guid? subjectId = null, int page = 1) =>
            s.TimelineAsync(from, to, area, actorId, subjectId, page, ct));
        audit.MapGet("/corrections", (DateOnly from, DateOnly to, IAuditAccess s, CancellationToken ct, Guid? branchId = null, string? module = null, int page = 1) =>
            s.CorrectionsAsync(from, to, branchId, module, page, ct));
    }
}

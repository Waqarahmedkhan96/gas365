namespace Gas365.Application;

public sealed record NotificationDto(Guid Id, string Kind, string Title, string Body, Guid? BranchId, DateTimeOffset CreatedAt, bool Read);
public sealed record NotificationSummaryDto(int Unread);
public interface INotificationAccess
{
    Task<PageDto<NotificationDto>> PageAsync(int page, CancellationToken ct);
    Task<NotificationSummaryDto> SummaryAsync(CancellationToken ct);
    Task MarkReadAsync(Guid id, CancellationToken ct);
    Task MarkAllReadAsync(CancellationToken ct);
}
// Recomputes alerts for every tenant; safe to run concurrently on several API instances.
public interface INotificationSweeper { Task<int> SweepAsync(CancellationToken ct); }

public sealed record ImportFieldDto(string Key, string Label, bool Required, string Hint);
public sealed record ImportTypeDto(string Key, string Title, string Description, ImportFieldDto[] Fields);
public sealed record ImportPreviewRowDto(int RowNumber, string Status, Dictionary<string, string> Values, string[] Errors);
public sealed record ImportBatchDto(Guid Id, string Type, string FileName, string Status, string[] Headers, Dictionary<string, string?> Mapping,
    int RowCount, int ErrorCount, int CommittedCount, DateTimeOffset CreatedAt, DateTimeOffset? CommittedAt, uint Version, ImportPreviewRowDto[] Preview);
public sealed record ImportMappingRequest(Dictionary<string, string?> Mapping, uint Version);
public sealed record ImportCommitRequest(uint Version);
public interface IImportAccess
{
    ImportTypeDto[] Types();
    Task<ImportBatchDto> UploadAsync(string type, string fileName, Stream content, CancellationToken ct);
    Task<ImportBatchDto> BatchAsync(Guid id, CancellationToken ct);
    Task<PageDto<ImportBatchDto>> BatchesAsync(int page, CancellationToken ct);
    Task<ImportBatchDto> MapAsync(Guid id, ImportMappingRequest request, CancellationToken ct);
    Task<ImportBatchDto> CommitAsync(Guid id, ImportCommitRequest request, CancellationToken ct);
    Task<ExportFile> ErrorsAsync(Guid id, CancellationToken ct);
}

public sealed record AuditEntryDto(Guid Id, string Action, string Area, Guid? ActorId, string Actor, Guid? SubjectId, DateTimeOffset OccurredAt);
public sealed record CorrectionDto(string Module, string Kind, Guid OriginalId, Guid ReversalId, string Branch, DateOnly BusinessDate, decimal Amount, string Reason, string Actor, DateTimeOffset PostedAt);
public interface IAuditAccess
{
    Task<PageDto<AuditEntryDto>> TimelineAsync(DateOnly from, DateOnly to, string? area, Guid? actorId, Guid? subjectId, int page, CancellationToken ct);
    Task<PageDto<CorrectionDto>> CorrectionsAsync(DateOnly from, DateOnly to, Guid? branchId, string? module, int page, CancellationToken ct);
}

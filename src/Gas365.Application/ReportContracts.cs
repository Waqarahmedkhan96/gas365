namespace Gas365.Application;

// Type is text, date, money, quantity, count or percent; clients format cells by column type.
public sealed record ReportColumn(string Key, string Label, string Type);
public sealed record ReportDefinitionDto(string Key, string Title, string Definition, string Reconciliation, string[] Types, bool DateRange, bool BranchScoped);
// From/To are inclusive tenant business dates. Point-in-time reports use To as the as-of date.
public sealed record ReportFilter(Guid? BranchId, DateOnly From, DateOnly To, string? Type);
// Cells are strings, decimals, integers or null. Totals align with Columns; non-additive columns are null.
public sealed record ReportDto(string Key, string Title, string Definition, ReportFilter Filter, string Scope, ReportColumn[] Columns,
    object?[][] Rows, object?[] Totals, int TotalRows, int Page, int PageSize, DateTimeOffset GeneratedAt);
public sealed record DashboardMetricDto(string Key, string Label, decimal Value, string Type);
public sealed record DashboardPointDto(string Date, decimal Revenue, decimal? GrossProfit, decimal? Expenses);
public sealed record DashboardSliceDto(string Label, decimal Value);
public sealed record DashboardDto(DateOnly From, DateOnly To, string Scope, DashboardMetricDto[] Metrics, DashboardPointDto[] Trend,
    DashboardSliceDto[] ExpensesByCategory, DashboardSliceDto[] RevenueByBranch, bool ProfitVisible, bool FinanceVisible);
public sealed record ExportRequest(string ReportKey, string Format, Guid? BranchId, DateOnly From, DateOnly To, string? Type, string IdempotencyKey);
public sealed record ExportJobDto(Guid Id, string ReportKey, string Format, string Status, int Attempts, string Error, string FileName, int RowCount,
    DateTimeOffset RequestedAt, DateTimeOffset? CompletedAt, DateTimeOffset? ExpiresAt);
public sealed record ExportFile(string FileName, string ContentType, byte[] Content);

public interface IReportAccess
{
    ReportDefinitionDto[] Definitions();
    Task<ReportDto> RunAsync(string key, ReportFilter filter, int page, CancellationToken ct);
    Task<DashboardDto> DashboardAsync(Guid? branchId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<ExportJobDto> RequestExportAsync(ExportRequest request, CancellationToken ct);
    Task<PageDto<ExportJobDto>> ExportsAsync(int page, CancellationToken ct);
    Task<ExportFile> DownloadAsync(Guid id, CancellationToken ct);
}

// Claims and runs queued exports outside a request. Returns false when no work was available.
public interface IExportProcessor
{
    Task<bool> ProcessNextAsync(CancellationToken ct);
    Task<int> ExpireAsync(CancellationToken ct);
}

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gas365.Api;

public sealed record HealthResponse(string Status, string Service, string Database)
{
    public static HealthResponse From(HealthReport report) => new(
        report.Status.ToString(),
        "Gas365 API",
        report.Entries.TryGetValue("postgres", out var entry)
            ? entry.Status.ToString()
            : "Unknown");
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gas365.Infrastructure;

// Degraded (not unhealthy) when a background job is failing or stalled, so monitoring alerts without restarting the API.
public sealed class JobsHealthCheck(Gas365DbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        Domain.JobState[] states;
        // Database reachability is covered by the postgres check; unreadable job health only degrades readiness.
        try { states = await db.JobStates.AsNoTracking().ToArrayAsync(cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException) { return HealthCheckResult.Degraded("Job health is unavailable."); }
        var now = DateTimeOffset.UtcNow;
        var failing = states.Where(s => s.ConsecutiveFailures >= 3).Select(s => s.Name);
        var stalled = states.Where(s => s.Name == "notifications" && s.LastSucceededAt < now.AddMinutes(-15)).Select(s => s.Name);
        var problems = failing.Concat(stalled).Distinct().ToArray();
        return problems.Length == 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Degraded("Background jobs need attention: " + string.Join(", ", problems));
    }
}

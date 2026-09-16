using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Gas365.Infrastructure;

public sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            // Health responses intentionally omit server names and connection details.
            return HealthCheckResult.Unhealthy("Database unavailable.");
        }
    }
}

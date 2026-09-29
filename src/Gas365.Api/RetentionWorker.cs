using Gas365.Infrastructure;

namespace Gas365.Api;

public sealed class RetentionWorker(IServiceScopeFactory scopes, ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Exception? failure = null;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<RetentionService>().RunAsync(stoppingToken);
                logger.LogInformation("Retention removed {Notifications} notifications, {Sessions} sessions and {Rows} import rows.", result.Notifications, result.Sessions, result.ImportRows);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { failure = exception; logger.LogError(exception, "Retention run failed."); }
            try { await using var health = scopes.CreateAsyncScope(); await JobHealth.RecordAsync(health.ServiceProvider.GetRequiredService<Gas365DbContext>(), "retention", failure, stoppingToken); }
            catch (Exception exception) when (exception is not OperationCanceledException) { logger.LogWarning(exception, "Could not record job health."); }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }
}

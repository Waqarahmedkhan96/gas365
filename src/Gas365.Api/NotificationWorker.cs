using Gas365.Application;
using Gas365.Infrastructure;

namespace Gas365.Api;

// Rebuilds low-stock, overdue-credit and open-register alerts on a fixed interval. A failed sweep is recorded and
// simply retried on the next tick; the sweep itself is idempotent.
public sealed class NotificationWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(configuration.GetValue("Notifications:SweepSeconds", 60));
        while (!stoppingToken.IsCancellationRequested)
        {
            Exception? failure = null;
            await using var scope = scopes.CreateAsyncScope();
            try { await scope.ServiceProvider.GetRequiredService<INotificationSweeper>().SweepAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { failure = exception; logger.LogError(exception, "Notification sweep failed."); }
            try
            {
                await using var health = scopes.CreateAsyncScope();
                await JobHealth.RecordAsync(health.ServiceProvider.GetRequiredService<Gas365DbContext>(), "notifications", failure, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException) { logger.LogWarning(exception, "Could not record job health."); }
            await Task.Delay(interval, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }
}

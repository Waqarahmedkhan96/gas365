using Gas365.Application;

namespace Gas365.Api;

// Polls PostgreSQL for queued exports. Several API instances can run it: jobs are claimed with SKIP LOCKED.
public sealed class ExportWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<ExportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idle = TimeSpan.FromSeconds(configuration.GetValue("Exports:PollSeconds", 2));
        var nextExpiry = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            var worked = false;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IExportProcessor>();
                if (DateTimeOffset.UtcNow >= nextExpiry) { await processor.ExpireAsync(stoppingToken); nextExpiry = DateTimeOffset.UtcNow.AddMinutes(5); }
                worked = await processor.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Export worker iteration failed."); }
            if (!worked) await Task.Delay(idle, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }
}

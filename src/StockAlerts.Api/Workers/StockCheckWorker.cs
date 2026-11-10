using Microsoft.Extensions.Options;
using StockAlerts.Core.StockChecks;

namespace StockAlerts.Api.Workers;

/// <summary>
/// Runs <see cref="StockCheckService"/> on a timer. It only schedules: every rule lives in the service
/// and in <c>Listing.RecordCheck</c>, where it can be tested without a host.
/// </summary>
public sealed class StockCheckWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<StockCheckOptions> options,
    ILogger<StockCheckWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Stock check worker is disabled");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds));

        do
        {
            try
            {
                // The service uses a DbContext, which is scoped; a hosted service is a singleton.
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<StockCheckService>();

                var summary = await service.RunAsync(stoppingToken);

                logger.LogInformation(
                    "Stock check finished: {Checked} checked, {Failed} failed, {Restocked} restocked, {AlertsCreated} alerts created",
                    summary.Checked, summary.Failed, summary.Restocked, summary.AlertsCreated);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // An unhandled exception here would stop the whole host. One bad cycle is not worth that.
                logger.LogError(ex, "Stock check cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

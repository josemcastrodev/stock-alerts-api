using Microsoft.Extensions.Logging;
using StockAlerts.Core.Entities;

namespace StockAlerts.Core.StockChecks;

/// <summary>
/// One pass over every listing: ask the source, apply the result, and alert subscribers on a confirmed restock.
/// </summary>
public sealed class StockCheckService(
    IStockCheckRepository repository,
    IStockSource stockSource,
    TimeProvider clock,
    ILogger<StockCheckService> logger)
{
    public async Task<StockCheckSummary> RunAsync(CancellationToken cancellationToken)
    {
        var listings = await repository.GetListingsAsync(cancellationToken);
        int failed = 0, restocked = 0, alertsCreated = 0;

        foreach (var listing in listings)
        {
            var result = await CheckAsync(listing, cancellationToken);
            var now = clock.GetUtcNow();

            var isRestock = listing.RecordCheck(result, now);
            var alerts = isRestock ? await CreateAlertsAsync(listing, now, cancellationToken) : [];

            // Status and alerts go in the same save: a restock is recorded together with its alerts or not at all.
            if (!await repository.TrySaveAsync(listing, alerts, cancellationToken))
            {
                logger.LogWarning("Listing {ListingId} changed during the check; result discarded", listing.Id);
                continue;
            }

            if (result.Outcome == StockCheckOutcome.Failed)
            {
                failed++;
                logger.LogWarning(
                    "Stock check failed for listing {ListingId} ({ConsecutiveFailures} in a row): {Error}. Status stays {Status}",
                    listing.Id, listing.ConsecutiveFailures, result.Error, listing.Status);
            }
            else if (isRestock)
            {
                restocked++;
                alertsCreated += alerts.Count;
                logger.LogInformation(
                    "Listing {ListingId} is back in stock; alerts created: {AlertCount}", listing.Id, alerts.Count);
            }
        }

        return new StockCheckSummary(listings.Count, failed, restocked, alertsCreated);
    }

    private async Task<StockCheckResult> CheckAsync(Listing listing, CancellationToken cancellationToken)
    {
        try
        {
            return await stockSource.CheckAsync(listing, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A source that throws is just another failed check. It must not stop the remaining listings.
            return StockCheckResult.Failed(ex.Message);
        }
    }

    private async Task<IReadOnlyCollection<Alert>> CreateAlertsAsync(
        Listing listing, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var subscriberIds = await repository.GetSubscriberIdsAsync(listing.ProductId, cancellationToken);
        return subscriberIds.Select(userId => Alert.ForRestock(userId, listing, now)).ToList();
    }
}

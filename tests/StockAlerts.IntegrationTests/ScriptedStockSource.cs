using System.Collections.Concurrent;
using StockAlerts.Core.Entities;
using StockAlerts.Core.StockChecks;

namespace StockAlerts.IntegrationTests;

/// <summary>
/// Returns, for each listing, the results a test queued for it. Listings without a script (created by
/// other tests sharing the database) just fail their check, which by design changes nothing.
/// </summary>
public sealed class ScriptedStockSource : IStockSource
{
    private readonly ConcurrentDictionary<Guid, Queue<StockCheckResult>> _results = new();

    public void Script(Guid listingId, params StockCheckResult[] results) =>
        _results[listingId] = new Queue<StockCheckResult>(results);

    public Task<StockCheckResult> CheckAsync(Listing listing, CancellationToken cancellationToken)
    {
        var result = _results.TryGetValue(listing.Id, out var queue) && queue.TryDequeue(out var next)
            ? next
            : StockCheckResult.Failed("No scripted result");

        return Task.FromResult(result);
    }
}

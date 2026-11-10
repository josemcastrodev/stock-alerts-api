using System.Collections.Concurrent;
using StockAlerts.Core.Entities;
using StockAlerts.Core.StockChecks;

namespace StockAlerts.Infrastructure.StockSources;

/// <summary>
/// Stand-in for a real scraper: no network, no randomness. Every listing replays the same script, starting
/// at a point derived from its URL, so a given catalog always produces the same sequence of results.
/// </summary>
public sealed class SimulatedStockSource : IStockSource
{
    // The two failures sit where a naive implementation breaks: one while out of stock (it must not hide
    // the restock that follows) and one while in stock (it must not cause a second, false restock).
    private static readonly StockCheckOutcome[] Script =
    [
        StockCheckOutcome.OutOfStock,
        StockCheckOutcome.Failed,
        StockCheckOutcome.InStock,
        StockCheckOutcome.InStock,
        StockCheckOutcome.Failed,
        StockCheckOutcome.InStock,
        StockCheckOutcome.OutOfStock,
        StockCheckOutcome.OutOfStock,
    ];

    private readonly ConcurrentDictionary<Guid, int> _checksByListing = new();

    public Task<StockCheckResult> CheckAsync(Listing listing, CancellationToken cancellationToken)
    {
        var seed = listing.Url.Sum(c => (int)c);
        var check = _checksByListing.AddOrUpdate(listing.Id, 0, (_, previous) => previous + 1);

        var result = Script[(seed + check) % Script.Length] switch
        {
            StockCheckOutcome.InStock => StockCheckResult.InStock(price: 19.95m + seed % 80),
            StockCheckOutcome.OutOfStock => StockCheckResult.OutOfStock(),
            _ => StockCheckResult.Failed("Simulated timeout"),
        };

        return Task.FromResult(result);
    }
}

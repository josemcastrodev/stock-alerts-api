using StockAlerts.Core.Entities;

namespace StockAlerts.Core.StockChecks;

/// <summary>Knows how to find out whether a listing is in stock (a scraper, a store API, a simulation...).</summary>
public interface IStockSource
{
    /// <summary>
    /// Returns <see cref="StockCheckResult.Failed"/> when it cannot tell. It must never guess "out of stock".
    /// </summary>
    Task<StockCheckResult> CheckAsync(Listing listing, CancellationToken cancellationToken);
}

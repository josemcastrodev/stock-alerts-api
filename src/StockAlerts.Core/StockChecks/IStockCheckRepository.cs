using StockAlerts.Core.Entities;

namespace StockAlerts.Core.StockChecks;

/// <summary>The persistence <see cref="StockCheckService"/> needs, and nothing else.</summary>
public interface IStockCheckRepository
{
    Task<IReadOnlyList<Listing>> GetListingsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetSubscriberIdsAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Saves the listing and its new alerts atomically.</summary>
    /// <returns>
    /// <c>false</c> when the listing changed underneath us (another instance already recorded a status
    /// change, or it was deleted). In that case nothing is saved.
    /// </returns>
    Task<bool> TrySaveAsync(Listing listing, IReadOnlyCollection<Alert> newAlerts, CancellationToken cancellationToken);
}

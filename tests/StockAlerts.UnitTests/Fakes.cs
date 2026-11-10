using StockAlerts.Core.Entities;
using StockAlerts.Core.StockChecks;

namespace StockAlerts.UnitTests;

internal sealed class FakeStockCheckRepository : IStockCheckRepository
{
    private readonly List<Listing> _listings = [];
    private readonly List<(Guid ProductId, Guid UserId)> _subscriptions = [];

    public List<Alert> SavedAlerts { get; } = [];

    /// <summary>Simulates another instance having changed the listing first.</summary>
    public bool RejectSaves { get; set; }

    public Listing AddListing(Guid? productId = null)
    {
        var listing = new Listing(Guid.NewGuid(), productId ?? Guid.NewGuid(), "https://store.example/item");
        _listings.Add(listing);
        return listing;
    }

    public Guid AddSubscriber(Guid productId)
    {
        var userId = Guid.NewGuid();
        _subscriptions.Add((productId, userId));
        return userId;
    }

    public Task<IReadOnlyList<Listing>> GetListingsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Listing>>(_listings);

    public Task<IReadOnlyList<Guid>> GetSubscriberIdsAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            _subscriptions.Where(s => s.ProductId == productId).Select(s => s.UserId).ToList());

    public Task<bool> TrySaveAsync(
        Listing listing, IReadOnlyCollection<Alert> newAlerts, CancellationToken cancellationToken)
    {
        if (RejectSaves)
        {
            return Task.FromResult(false);
        }

        SavedAlerts.AddRange(newAlerts);
        return Task.FromResult(true);
    }
}

/// <summary>Returns, for each listing, the results it was given, one per check.</summary>
internal sealed class ScriptedStockSource : IStockSource
{
    private readonly Dictionary<Guid, Queue<StockCheckResult>> _results = [];
    private readonly HashSet<Guid> _throwing = [];

    public void Script(Listing listing, params StockCheckResult[] results) =>
        _results[listing.Id] = new Queue<StockCheckResult>(results);

    public void ThrowFor(Listing listing) => _throwing.Add(listing.Id);

    public Task<StockCheckResult> CheckAsync(Listing listing, CancellationToken cancellationToken)
    {
        if (_throwing.Contains(listing.Id))
        {
            throw new HttpRequestException("Connection refused");
        }

        return Task.FromResult(_results[listing.Id].Dequeue());
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

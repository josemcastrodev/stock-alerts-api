namespace StockAlerts.Core.Entities;

/// <summary>Tells one user that one listing is back in stock.</summary>
public sealed class Alert
{
    private Alert(Guid userId, Guid listingId, decimal? price, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        ListingId = listingId;
        Price = price;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ListingId { get; private set; }

    /// <summary>Price at the moment of the restock; the listing's price may change later.</summary>
    public decimal? Price { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public Listing Listing { get; private set; } = null!;

    public static Alert ForRestock(Guid userId, Listing listing, DateTimeOffset restockedAt) =>
        new(userId, listing.Id, listing.Price, restockedAt);

    public void MarkAsRead(DateTimeOffset readAt) => ReadAt ??= readAt;
}

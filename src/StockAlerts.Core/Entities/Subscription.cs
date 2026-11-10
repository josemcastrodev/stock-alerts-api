namespace StockAlerts.Core.Entities;

/// <summary>A user's request to be alerted when a product is back in stock in any store.</summary>
public sealed class Subscription
{
    public Subscription(Guid userId, Guid productId, DateTimeOffset createdAt)
    {
        UserId = userId;
        ProductId = productId;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Product Product { get; private set; } = null!;
}

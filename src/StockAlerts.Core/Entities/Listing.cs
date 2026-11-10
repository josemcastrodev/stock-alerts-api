using StockAlerts.Core.StockChecks;

namespace StockAlerts.Core.Entities;

/// <summary>A product as sold by one store. This is the unit whose stock is checked.</summary>
public sealed class Listing
{
    public Listing(Guid storeId, Guid productId, string url)
    {
        Id = Guid.NewGuid();
        StoreId = storeId;
        ProductId = productId;
        Url = url;
    }

    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Url { get; private set; }

    public StockStatus Status { get; private set; } = StockStatus.Unknown;

    /// <summary>Last price seen while in stock.</summary>
    public decimal? Price { get; private set; }

    /// <summary>When <see cref="Status"/> was last confirmed. Failed checks do not move it.</summary>
    public DateTimeOffset? LastCheckedAt { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public Store Store { get; private set; } = null!;
    public Product Product { get; private set; } = null!;

    /// <summary>Applies the outcome of a stock check.</summary>
    /// <returns><c>true</c> only when the check confirms a restock (OutOfStock to InStock).</returns>
    public bool RecordCheck(StockCheckResult result, DateTimeOffset checkedAt)
    {
        if (result.Outcome == StockCheckOutcome.Failed)
        {
            // A failed check says nothing about stock, so the status must not move. Treating a failure
            // as OutOfStock would make the next successful check look like a restock and alert everyone.
            ConsecutiveFailures++;
            return false;
        }

        var previous = Status;

        Status = result.Outcome == StockCheckOutcome.InStock ? StockStatus.InStock : StockStatus.OutOfStock;
        Price = result.Price ?? Price;
        LastCheckedAt = checkedAt;
        ConsecutiveFailures = 0;

        // Unknown to InStock is the first observation of a listing, not a restock.
        return previous == StockStatus.OutOfStock && Status == StockStatus.InStock;
    }
}

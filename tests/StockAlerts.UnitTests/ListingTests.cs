using StockAlerts.Core.Entities;
using StockAlerts.Core.StockChecks;

namespace StockAlerts.UnitTests;

public class ListingTests
{
    private static readonly DateTimeOffset Earlier = new(2025, 3, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Earlier.AddMinutes(5);

    [Fact]
    public void New_listing_has_unknown_status()
    {
        var listing = NewListing();

        Assert.Equal(StockStatus.Unknown, listing.Status);
        Assert.Null(listing.Price);
        Assert.Null(listing.LastCheckedAt);
    }

    [Fact]
    public void Successful_check_records_status_price_and_time()
    {
        var listing = NewListing();

        listing.RecordCheck(StockCheckResult.InStock(49.90m), Now);

        Assert.Equal(StockStatus.InStock, listing.Status);
        Assert.Equal(49.90m, listing.Price);
        Assert.Equal(Now, listing.LastCheckedAt);
    }

    [Fact]
    public void Out_of_stock_to_in_stock_is_a_restock()
    {
        var listing = ListingAfter(StockCheckResult.OutOfStock());

        var isRestock = listing.RecordCheck(StockCheckResult.InStock(49.90m), Now);

        Assert.True(isRestock);
    }

    [Fact]
    public void First_time_seen_in_stock_is_not_a_restock()
    {
        var listing = NewListing();

        var isRestock = listing.RecordCheck(StockCheckResult.InStock(49.90m), Now);

        Assert.False(isRestock);
    }

    [Fact]
    public void Staying_in_stock_is_not_a_restock()
    {
        var listing = ListingAfter(StockCheckResult.OutOfStock(), StockCheckResult.InStock(49.90m));

        var isRestock = listing.RecordCheck(StockCheckResult.InStock(49.90m), Now);

        Assert.False(isRestock);
    }

    [Fact]
    public void Every_restock_is_reported()
    {
        var listing = ListingAfter(
            StockCheckResult.OutOfStock(),
            StockCheckResult.InStock(49.90m),
            StockCheckResult.OutOfStock());

        var isRestock = listing.RecordCheck(StockCheckResult.InStock(49.90m), Now);

        Assert.True(isRestock);
    }

    [Theory]
    [InlineData(StockStatus.Unknown)]
    [InlineData(StockStatus.InStock)]
    [InlineData(StockStatus.OutOfStock)]
    public void Failed_check_never_changes_the_status(StockStatus status)
    {
        var listing = ListingIn(status);

        var isRestock = listing.RecordCheck(StockCheckResult.Failed("timeout"), Now);

        Assert.False(isRestock);
        Assert.Equal(status, listing.Status);
    }

    [Fact]
    public void Failed_check_keeps_the_last_confirmed_price_and_time()
    {
        var listing = NewListing();
        listing.RecordCheck(StockCheckResult.InStock(49.90m), Earlier);

        listing.RecordCheck(StockCheckResult.Failed("timeout"), Now);

        Assert.Equal(49.90m, listing.Price);
        Assert.Equal(Earlier, listing.LastCheckedAt);
    }

    // The production bug this project is built around: the failure was stored as "out of stock",
    // so the next successful check looked like a restock and every subscriber got a false alert.
    [Fact]
    public void Failure_while_in_stock_does_not_cause_a_false_restock()
    {
        var listing = ListingAfter(StockCheckResult.InStock(49.90m), StockCheckResult.Failed("timeout"));

        var isRestock = listing.RecordCheck(StockCheckResult.InStock(49.90m), Now);

        Assert.False(isRestock);
    }

    [Fact]
    public void Failure_while_out_of_stock_does_not_hide_the_restock()
    {
        var listing = ListingAfter(StockCheckResult.OutOfStock(), StockCheckResult.Failed("timeout"));

        var isRestock = listing.RecordCheck(StockCheckResult.InStock(49.90m), Now);

        Assert.True(isRestock);
    }

    [Fact]
    public void Consecutive_failures_are_counted_and_reset_by_a_successful_check()
    {
        var listing = ListingAfter(StockCheckResult.Failed("timeout"), StockCheckResult.Failed("timeout"));
        Assert.Equal(2, listing.ConsecutiveFailures);

        listing.RecordCheck(StockCheckResult.OutOfStock(), Now);

        Assert.Equal(0, listing.ConsecutiveFailures);
    }

    [Fact]
    public void Going_out_of_stock_keeps_the_last_known_price()
    {
        var listing = ListingAfter(StockCheckResult.InStock(49.90m));

        listing.RecordCheck(StockCheckResult.OutOfStock(), Now);

        Assert.Equal(StockStatus.OutOfStock, listing.Status);
        Assert.Equal(49.90m, listing.Price);
    }

    private static Listing NewListing() =>
        new(storeId: Guid.NewGuid(), productId: Guid.NewGuid(), url: "https://store.example/item");

    private static Listing ListingAfter(params StockCheckResult[] history)
    {
        var listing = NewListing();
        foreach (var result in history)
        {
            listing.RecordCheck(result, Earlier);
        }

        return listing;
    }

    private static Listing ListingIn(StockStatus status) => status switch
    {
        StockStatus.InStock => ListingAfter(StockCheckResult.InStock(49.90m)),
        StockStatus.OutOfStock => ListingAfter(StockCheckResult.OutOfStock()),
        _ => NewListing(),
    };
}

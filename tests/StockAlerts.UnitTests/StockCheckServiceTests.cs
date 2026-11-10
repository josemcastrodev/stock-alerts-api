using Microsoft.Extensions.Logging.Abstractions;
using StockAlerts.Core.Entities;
using StockAlerts.Core.StockChecks;

namespace StockAlerts.UnitTests;

public class StockCheckServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 3, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeStockCheckRepository _repository = new();
    private readonly ScriptedStockSource _source = new();
    private readonly StockCheckService _service;

    public StockCheckServiceTests()
    {
        _service = new StockCheckService(
            _repository, _source, new FixedTimeProvider(Now), NullLogger<StockCheckService>.Instance);
    }

    [Fact]
    public async Task Restock_creates_one_alert_for_each_subscriber_of_that_product()
    {
        var listing = _repository.AddListing();
        var ana = _repository.AddSubscriber(listing.ProductId);
        var luis = _repository.AddSubscriber(listing.ProductId);
        _repository.AddSubscriber(productId: Guid.NewGuid());
        _source.Script(listing, StockCheckResult.OutOfStock(), StockCheckResult.InStock(49.90m));

        await RunAsync(times: 1);
        var summary = await _service.RunAsync(CancellationToken.None);

        Assert.Equal(new StockCheckSummary(Checked: 1, Failed: 0, Restocked: 1, AlertsCreated: 2), summary);
        Assert.Equal([ana, luis], _repository.SavedAlerts.Select(a => a.UserId));
        Assert.All(_repository.SavedAlerts, alert =>
        {
            Assert.Equal(listing.Id, alert.ListingId);
            Assert.Equal(49.90m, alert.Price);
            Assert.Equal(Now, alert.CreatedAt);
            Assert.Null(alert.ReadAt);
        });
    }

    [Fact]
    public async Task First_time_seen_in_stock_creates_no_alert()
    {
        var listing = ListingWithSubscriber();
        _source.Script(listing, StockCheckResult.InStock(49.90m));

        await RunAsync(times: 1);

        Assert.Empty(_repository.SavedAlerts);
    }

    [Fact]
    public async Task Staying_in_stock_does_not_alert_again()
    {
        var listing = ListingWithSubscriber();
        _source.Script(
            listing,
            StockCheckResult.OutOfStock(),
            StockCheckResult.InStock(49.90m),
            StockCheckResult.InStock(49.90m),
            StockCheckResult.InStock(49.90m));

        await RunAsync(times: 4);

        Assert.Single(_repository.SavedAlerts);
    }

    [Fact]
    public async Task Failed_check_while_in_stock_does_not_create_a_second_alert()
    {
        var listing = ListingWithSubscriber();
        _source.Script(
            listing,
            StockCheckResult.OutOfStock(),
            StockCheckResult.InStock(49.90m),
            StockCheckResult.Failed("timeout"),
            StockCheckResult.InStock(49.90m));

        await RunAsync(times: 4);

        Assert.Single(_repository.SavedAlerts);
    }

    [Fact]
    public async Task Failed_check_while_out_of_stock_does_not_hide_the_restock()
    {
        var listing = ListingWithSubscriber();
        _source.Script(
            listing,
            StockCheckResult.OutOfStock(),
            StockCheckResult.Failed("timeout"),
            StockCheckResult.InStock(49.90m));

        await RunAsync(times: 3);

        Assert.Single(_repository.SavedAlerts);
    }

    [Fact]
    public async Task Each_new_restock_gets_its_own_alert()
    {
        var listing = ListingWithSubscriber();
        _source.Script(
            listing,
            StockCheckResult.OutOfStock(),
            StockCheckResult.InStock(49.90m),
            StockCheckResult.OutOfStock(),
            StockCheckResult.InStock(44.90m));

        await RunAsync(times: 4);

        Assert.Equal([49.90m, 44.90m], _repository.SavedAlerts.Select(a => a.Price));
    }

    [Fact]
    public async Task Source_exception_is_a_failed_check_and_does_not_stop_the_other_listings()
    {
        var broken = _repository.AddListing();
        var healthy = _repository.AddListing();
        _source.ThrowFor(broken);
        _source.Script(healthy, StockCheckResult.OutOfStock());

        var summary = await _service.RunAsync(CancellationToken.None);

        Assert.Equal(new StockCheckSummary(Checked: 2, Failed: 1, Restocked: 0, AlertsCreated: 0), summary);
        Assert.Equal(StockStatus.Unknown, broken.Status);
        Assert.Equal(1, broken.ConsecutiveFailures);
        Assert.Equal(StockStatus.OutOfStock, healthy.Status);
    }

    [Fact]
    public async Task Restock_whose_save_is_rejected_is_not_reported()
    {
        var listing = ListingWithSubscriber();
        _source.Script(listing, StockCheckResult.OutOfStock(), StockCheckResult.InStock(49.90m));
        await RunAsync(times: 1);

        _repository.RejectSaves = true;
        var summary = await _service.RunAsync(CancellationToken.None);

        Assert.Equal(new StockCheckSummary(Checked: 1, Failed: 0, Restocked: 0, AlertsCreated: 0), summary);
        Assert.Empty(_repository.SavedAlerts);
    }

    private Listing ListingWithSubscriber()
    {
        var listing = _repository.AddListing();
        _repository.AddSubscriber(listing.ProductId);
        return listing;
    }

    private async Task RunAsync(int times)
    {
        for (var i = 0; i < times; i++)
        {
            await _service.RunAsync(CancellationToken.None);
        }
    }
}

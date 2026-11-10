using System.Net;
using StockAlerts.Api.Contracts;
using StockAlerts.Core.Entities;
using StockAlerts.Core.StockChecks;

namespace StockAlerts.IntegrationTests;

public class RestockFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Subscriber_gets_exactly_one_alert_when_the_product_is_confirmed_back_in_stock()
    {
        var (product, listing) = await CreateListedProductAsync("Ember Skies Booster Box");
        var subscriber = await factory.CreateUserClientAsync();
        var bystander = await factory.CreateUserClientAsync();
        await SubscribeAsync(subscriber, product.Id);

        factory.StockSource.Script(
            listing.Id,
            StockCheckResult.OutOfStock(),
            StockCheckResult.Failed("timeout"),
            StockCheckResult.InStock(49.90m),
            StockCheckResult.Failed("timeout"),
            StockCheckResult.InStock(49.90m));

        // Out of stock, then a failed check: nothing to tell anyone, and the status is still OutOfStock.
        await factory.RunStockCheckAsync();
        await factory.RunStockCheckAsync();
        Assert.Empty((await GetAlertsAsync(subscriber)).Items);
        Assert.Equal(StockStatus.OutOfStock, (await GetListingAsync(product.Id)).Status);

        // Confirmed restock.
        await factory.RunStockCheckAsync();
        var alert = Assert.Single((await GetAlertsAsync(subscriber)).Items);
        Assert.Equal(product.Id, alert.ProductId);
        Assert.Equal("Ember Skies Booster Box", alert.ProductName);
        Assert.Equal(listing.Url, alert.Url);
        Assert.Equal(49.90m, alert.Price);
        Assert.Null(alert.ReadAt);

        // Another failure and another in-stock check: still the same restock, so still one alert.
        await factory.RunStockCheckAsync();
        await factory.RunStockCheckAsync();
        Assert.Single((await GetAlertsAsync(subscriber)).Items);
        Assert.Equal(StockStatus.InStock, (await GetListingAsync(product.Id)).Status);

        Assert.Empty((await GetAlertsAsync(bystander)).Items);
    }

    [Fact]
    public async Task Alerts_can_be_marked_as_read_only_by_their_owner()
    {
        var (product, listing) = await CreateListedProductAsync("Tidal Realms Bundle");
        var owner = await factory.CreateUserClientAsync();
        var someoneElse = await factory.CreateUserClientAsync();
        await SubscribeAsync(owner, product.Id);
        factory.StockSource.Script(listing.Id, StockCheckResult.OutOfStock(), StockCheckResult.InStock(24.95m));
        await factory.RunStockCheckAsync();
        await factory.RunStockCheckAsync();
        var alert = Assert.Single((await GetAlertsAsync(owner)).Items);

        var foreignAttempt = await someoneElse.PostAsync($"/api/alerts/{alert.Id}/read", content: null);
        var ownAttempt = await owner.PostAsync($"/api/alerts/{alert.Id}/read", content: null);

        Assert.Equal(HttpStatusCode.NotFound, foreignAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ownAttempt.StatusCode);
        Assert.NotNull(Assert.Single((await GetAlertsAsync(owner)).Items).ReadAt);
        Assert.Empty((await GetAlertsAsync(owner, unreadOnly: true)).Items);
    }

    [Fact]
    public async Task Unsubscribed_user_gets_no_alert()
    {
        var (product, listing) = await CreateListedProductAsync("Starter Deck");
        var user = await factory.CreateUserClientAsync();
        await SubscribeAsync(user, product.Id);
        var unsubscribe = await user.DeleteAsync($"/api/subscriptions/{product.Id}");
        factory.StockSource.Script(listing.Id, StockCheckResult.OutOfStock(), StockCheckResult.InStock(14.95m));

        await factory.RunStockCheckAsync();
        await factory.RunStockCheckAsync();

        Assert.Equal(HttpStatusCode.NoContent, unsubscribe.StatusCode);
        Assert.Empty((await GetAlertsAsync(user)).Items);
    }

    private async Task<(ProductSummary Product, ListingResponse Listing)> CreateListedProductAsync(string productName)
    {
        var admin = await factory.CreateAdminClientAsync();
        var store = await admin.CreateAsync<StoreResponse>("/api/stores", new StoreRequest("Mana Vault", "https://mana-vault.example"));
        var product = await admin.CreateAsync<ProductSummary>("/api/products", new ProductRequest(productName, "Sealed"));
        var listing = await admin.CreateAsync<ListingResponse>(
            "/api/listings", new CreateListingRequest(store.Id, product.Id, $"https://mana-vault.example/{product.Id}"));

        return (product, listing);
    }

    private static async Task SubscribeAsync(HttpClient client, Guid productId)
    {
        var response = await client.PutAsync($"/api/subscriptions/{productId}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static Task<PagedResponse<AlertResponse>> GetAlertsAsync(HttpClient client, bool unreadOnly = false) =>
        client.GetAsync<PagedResponse<AlertResponse>>($"/api/alerts?unreadOnly={unreadOnly}");

    private async Task<ListingResponse> GetListingAsync(Guid productId)
    {
        var product = await factory.CreateClient().GetAsync<ProductDetail>($"/api/products/{productId}");
        return Assert.Single(product.Listings);
    }
}

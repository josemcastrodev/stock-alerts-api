using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockAlerts.Core.Entities;
using StockAlerts.Core.StockChecks;
using StockAlerts.Infrastructure.Persistence;

namespace StockAlerts.IntegrationTests;

public class ConcurrentStockCheckTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateTimeOffset Now = new(2025, 3, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_two_instances_detect_the_same_restock_only_one_saves_it_and_its_alerts()
    {
        var (listingId, userId) = await SeedOutOfStockListingWithSubscriberAsync();

        // Two scopes are two DbContexts: two workers that loaded the listing while it was out of stock.
        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var repositoryA = scopeA.ServiceProvider.GetRequiredService<IStockCheckRepository>();
        var repositoryB = scopeB.ServiceProvider.GetRequiredService<IStockCheckRepository>();
        var listingA = (await repositoryA.GetListingsAsync(CancellationToken.None)).Single(l => l.Id == listingId);
        var listingB = (await repositoryB.GetListingsAsync(CancellationToken.None)).Single(l => l.Id == listingId);

        Assert.True(listingA.RecordCheck(StockCheckResult.InStock(49.90m), Now));
        Assert.True(listingB.RecordCheck(StockCheckResult.InStock(49.90m), Now));

        var savedA = await repositoryA.TrySaveAsync(
            listingA, [Alert.ForRestock(userId, listingA, Now)], CancellationToken.None);
        var savedB = await repositoryB.TrySaveAsync(
            listingB, [Alert.ForRestock(userId, listingB, Now)], CancellationToken.None);

        Assert.True(savedA);
        Assert.False(savedB);

        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(dbB.ChangeTracker.HasChanges());
        Assert.Equal(1, await dbB.Alerts.CountAsync(a => a.ListingId == listingId));
    }

    private async Task<(Guid ListingId, Guid UserId)> SeedOutOfStockListingWithSubscriberAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var store = new Store("Mana Vault", "https://mana-vault.example");
        var product = new Product("Booster Box", "Boxes");
        var listing = new Listing(store.Id, product.Id, "https://mana-vault.example/booster-box");
        listing.RecordCheck(StockCheckResult.OutOfStock(), Now.AddMinutes(-5));
        var user = new User("subscriber@tests.local", UserRole.User, Now);

        db.AddRange(store, product, listing, user, new Subscription(user.Id, product.Id, Now));
        await db.SaveChangesAsync();

        return (listing.Id, user.Id);
    }
}

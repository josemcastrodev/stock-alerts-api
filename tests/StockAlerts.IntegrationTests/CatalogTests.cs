using System.Net;
using System.Net.Http.Json;
using StockAlerts.Api.Contracts;
using StockAlerts.Core.Entities;

namespace StockAlerts.IntegrationTests;

public class CatalogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Listing_created_by_an_admin_is_publicly_visible_under_its_product()
    {
        var admin = await factory.CreateAdminClientAsync();
        var store = await admin.CreateAsync<StoreResponse>("/api/stores", new StoreRequest("Mana Vault", "https://mana-vault.example"));
        var product = await admin.CreateAsync<ProductSummary>("/api/products", new ProductRequest("Booster Box", "Boxes"));
        var listing = await admin.CreateAsync<ListingResponse>(
            "/api/listings", new CreateListingRequest(store.Id, product.Id, "https://mana-vault.example/booster-box"));

        var anonymous = factory.CreateClient();
        var detail = await anonymous.GetAsync<ProductDetail>($"/api/products/{product.Id}");

        var visible = Assert.Single(detail.Listings);
        Assert.Equal(listing.Id, visible.Id);
        Assert.Equal("Mana Vault", visible.StoreName);
        Assert.Equal(StockStatus.Unknown, visible.Status);
    }

    [Fact]
    public async Task Products_are_paged_and_filtered()
    {
        var admin = await factory.CreateAdminClientAsync();
        var category = $"category-{Guid.NewGuid():N}";
        foreach (var name in new[] { "Alpha Box", "Beta Box", "Gamma Tin" })
        {
            await admin.CreateAsync<ProductSummary>("/api/products", new ProductRequest(name, category));
        }

        var client = factory.CreateClient();
        var firstPage = await client.GetAsync<PagedResponse<ProductSummary>>($"/api/products?category={category}&pageSize=2");
        var secondPage = await client.GetAsync<PagedResponse<ProductSummary>>($"/api/products?category={category}&pageSize=2&page=2");
        var search = await client.GetAsync<PagedResponse<ProductSummary>>($"/api/products?category={category}&search=tin");

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(["Alpha Box", "Beta Box"], firstPage.Items.Select(p => p.Name));
        Assert.Equal(["Gamma Tin"], secondPage.Items.Select(p => p.Name));
        Assert.Equal(["Gamma Tin"], search.Items.Select(p => p.Name));
    }

    [Fact]
    public async Task Invalid_paging_returns_400()
    {
        var response = await factory.CreateClient().GetAsync("/api/products?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Store_cannot_list_the_same_product_twice()
    {
        var admin = await factory.CreateAdminClientAsync();
        var store = await admin.CreateAsync<StoreResponse>("/api/stores", new StoreRequest("Dragon Den", "https://dragon-den.example"));
        var product = await admin.CreateAsync<ProductSummary>("/api/products", new ProductRequest("Elite Box", "Boxes"));
        var request = new CreateListingRequest(store.Id, product.Id, "https://dragon-den.example/elite-box");
        await admin.CreateAsync<ListingResponse>("/api/listings", request);

        var response = await admin.PostAsJsonAsync("/api/listings", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_store_removes_its_listings()
    {
        var admin = await factory.CreateAdminClientAsync();
        var store = await admin.CreateAsync<StoreResponse>("/api/stores", new StoreRequest("Closing Down", "https://closing.example"));
        var product = await admin.CreateAsync<ProductSummary>("/api/products", new ProductRequest("Last Box", "Boxes"));
        await admin.CreateAsync<ListingResponse>(
            "/api/listings", new CreateListingRequest(store.Id, product.Id, "https://closing.example/last-box"));

        var response = await admin.DeleteAsync($"/api/stores/{store.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await admin.GetAsync<ProductDetail>($"/api/products/{product.Id}")).Listings);
    }

    [Fact]
    public async Task Listing_for_a_store_that_does_not_exist_returns_400()
    {
        var admin = await factory.CreateAdminClientAsync();
        var product = await admin.CreateAsync<ProductSummary>("/api/products", new ProductRequest("Tin", "Tins"));

        var response = await admin.PostAsJsonAsync(
            "/api/listings", new CreateListingRequest(Guid.NewGuid(), product.Id, "https://nowhere.example/tin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

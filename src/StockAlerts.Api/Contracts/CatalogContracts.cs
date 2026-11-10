using System.ComponentModel.DataAnnotations;
using StockAlerts.Core.Entities;

namespace StockAlerts.Api.Contracts;

public sealed record StoreRequest(
    [Required, MaxLength(100)] string Name,
    [Required, Url, MaxLength(500)] string Website);

public sealed record StoreResponse(Guid Id, string Name, string Website);

public sealed record ProductRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(100)] string Category);

public sealed record ProductQuery : PageQuery
{
    /// <summary>Text contained in the product name.</summary>
    [MaxLength(200)]
    public string? Search { get; init; }

    [MaxLength(100)]
    public string? Category { get; init; }

    /// <summary>true: only products in stock in at least one store. false: only products in stock nowhere.</summary>
    public bool? InStock { get; init; }
}

public sealed record ProductSummary(Guid Id, string Name, string Category, bool InStock);

public sealed record ProductDetail(Guid Id, string Name, string Category, IReadOnlyList<ListingResponse> Listings);

public sealed record CreateListingRequest(
    Guid StoreId,
    Guid ProductId,
    [Required, Url, MaxLength(2000)] string Url);

public sealed record ListingResponse(
    Guid Id,
    Guid StoreId,
    string StoreName,
    string Url,
    StockStatus Status,
    decimal? Price,
    DateTimeOffset? LastCheckedAt);

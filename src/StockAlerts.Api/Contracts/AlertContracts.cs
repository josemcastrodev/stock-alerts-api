namespace StockAlerts.Api.Contracts;

public sealed record SubscriptionResponse(Guid ProductId, string ProductName, DateTimeOffset CreatedAt);

public sealed record AlertQuery : PageQuery
{
    public bool UnreadOnly { get; init; }
}

public sealed record AlertResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string StoreName,
    string Url,
    decimal? Price,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

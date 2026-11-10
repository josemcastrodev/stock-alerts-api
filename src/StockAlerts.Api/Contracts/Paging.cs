using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace StockAlerts.Api.Contracts;

public record PageQuery
{
    [Range(1, 10_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class PagingExtensions
{
    /// <summary>The query must already be ordered; paging an unordered query returns unstable pages.</summary>
    public static async Task<PagedResponse<T>> ToPagedResponseAsync<T>(
        this IQueryable<T> query, PageQuery page, CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<T>(items, page.Page, page.PageSize, totalCount);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockAlerts.Api.Contracts;
using StockAlerts.Core.Entities;
using StockAlerts.Infrastructure.Persistence;

namespace StockAlerts.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<ProductSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<PagedResponse<ProductSummary>> GetAll(
        [FromQuery] ProductQuery query, CancellationToken cancellationToken)
    {
        var products = db.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Lower-casing both sides keeps the search case-insensitive on SQLite as well; SQL Server
            // already is through its default collation.
            var search = query.Search.Trim().ToLower();
            products = products.Where(p => p.Name.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            products = products.Where(p => p.Category == query.Category);
        }

        if (query.InStock is { } inStock)
        {
            products = products.Where(p => p.Listings.Any(l => l.Status == StockStatus.InStock) == inStock);
        }

        return await products
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Select(p => new ProductSummary(
                p.Id, p.Name, p.Category, p.Listings.Any(l => l.Status == StockStatus.InStock)))
            .ToPagedResponseAsync(query, cancellationToken);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetail>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products
            .Where(p => p.Id == id)
            .Select(p => new ProductDetail(
                p.Id,
                p.Name,
                p.Category,
                p.Listings
                    .OrderBy(l => l.Store.Name)
                    .Select(l => new ListingResponse(
                        l.Id, l.StoreId, l.Store.Name, l.Url, l.Status, l.Price, l.LastCheckedAt))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return product is null ? NotFound() : product;
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<ProductSummary>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductSummary>> Create(ProductRequest request, CancellationToken cancellationToken)
    {
        var product = new Product(request.Name, request.Category);
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        var response = new ProductSummary(product.Id, product.Name, product.Category, InStock: false);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, ProductRequest request, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        product.Update(request.Name, request.Category);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Deletes the product together with its listings, subscriptions and alerts.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? NotFound() : NoContent();
    }
}

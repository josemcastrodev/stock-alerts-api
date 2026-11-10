using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockAlerts.Api.Contracts;
using StockAlerts.Core.Entities;
using StockAlerts.Infrastructure.Persistence;

namespace StockAlerts.Api.Controllers;

/// <summary>Listings are read through their product (GET /api/products/{id}); this controller only manages them.</summary>
[ApiController]
[Route("api/listings")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class ListingsController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ListingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ListingResponse>> Create(CreateListingRequest request, CancellationToken cancellationToken)
    {
        var store = await db.Stores.FindAsync([request.StoreId], cancellationToken);
        if (store is null)
        {
            ModelState.AddModelError(nameof(request.StoreId), "Store not found.");
        }

        if (!await db.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.ProductId), "Product not found.");
        }

        if (store is null || !ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var alreadyListed = await db.Listings.AnyAsync(
            l => l.StoreId == request.StoreId && l.ProductId == request.ProductId, cancellationToken);
        if (alreadyListed)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "That store already has a listing for this product.");
        }

        var listing = new Listing(request.StoreId, request.ProductId, request.Url);
        db.Listings.Add(listing);
        await db.SaveChangesAsync(cancellationToken);

        var response = new ListingResponse(
            listing.Id, store.Id, store.Name, listing.Url, listing.Status, listing.Price, listing.LastCheckedAt);

        return CreatedAtAction(
            nameof(ProductsController.GetById), "Products", new { id = listing.ProductId }, response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await db.Listings.Where(l => l.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? NotFound() : NoContent();
    }
}

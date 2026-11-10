using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockAlerts.Api.Contracts;
using StockAlerts.Core.Entities;
using StockAlerts.Infrastructure.Auth;
using StockAlerts.Infrastructure.Persistence;

namespace StockAlerts.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public sealed class SubscriptionsController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SubscriptionResponse>> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        return await db.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Product.Name)
            .Select(s => new SubscriptionResponse(s.ProductId, s.Product.Name, s.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Subscribes the current user to a product. Subscribing twice is not an error.</summary>
    [HttpPut("{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Subscribe(Guid productId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (!await db.Products.AnyAsync(p => p.Id == productId, cancellationToken))
        {
            return NotFound();
        }

        var alreadySubscribed = await db.Subscriptions.AnyAsync(
            s => s.UserId == userId && s.ProductId == productId, cancellationToken);
        if (!alreadySubscribed)
        {
            db.Subscriptions.Add(new Subscription(userId, productId, clock.GetUtcNow()));
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpDelete("{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unsubscribe(Guid productId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await db.Subscriptions
            .Where(s => s.UserId == userId && s.ProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);

        return NoContent();
    }
}

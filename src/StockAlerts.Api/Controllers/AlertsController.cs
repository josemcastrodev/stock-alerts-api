using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockAlerts.Api.Contracts;
using StockAlerts.Infrastructure.Auth;
using StockAlerts.Infrastructure.Persistence;

namespace StockAlerts.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public sealed class AlertsController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AlertResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<PagedResponse<AlertResponse>> GetMine(
        [FromQuery] AlertQuery query, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var alerts = db.Alerts.Where(a => a.UserId == userId);

        if (query.UnreadOnly)
        {
            alerts = alerts.Where(a => a.ReadAt == null);
        }

        return await alerts
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => new AlertResponse(
                a.Id,
                a.Listing.ProductId,
                a.Listing.Product.Name,
                a.Listing.Store.Name,
                a.Listing.Url,
                a.Price,
                a.CreatedAt,
                a.ReadAt))
            .ToPagedResponseAsync(query, cancellationToken);
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        // Filtering by user as well as id: someone else's alert is simply "not found".
        var alert = await db.Alerts.SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);
        if (alert is null)
        {
            return NotFound();
        }

        alert.MarkAsRead(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var now = clock.GetUtcNow();

        await db.Alerts
            .Where(a => a.UserId == userId && a.ReadAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(a => a.ReadAt, now), cancellationToken);

        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockAlerts.Api.Contracts;
using StockAlerts.Core.Entities;
using StockAlerts.Infrastructure.Persistence;

namespace StockAlerts.Api.Controllers;

[ApiController]
[Route("api/stores")]
public sealed class StoresController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<StoreResponse>> GetAll(CancellationToken cancellationToken) =>
        await db.Stores
            .OrderBy(s => s.Name)
            .Select(s => new StoreResponse(s.Id, s.Name, s.Website))
            .ToListAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<StoreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StoreResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var store = await db.Stores.FindAsync([id], cancellationToken);
        return store is null ? NotFound() : ToResponse(store);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<StoreResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StoreResponse>> Create(StoreRequest request, CancellationToken cancellationToken)
    {
        var store = new Store(request.Name, request.Website);
        db.Stores.Add(store);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = store.Id }, ToResponse(store));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, StoreRequest request, CancellationToken cancellationToken)
    {
        var store = await db.Stores.FindAsync([id], cancellationToken);
        if (store is null)
        {
            return NotFound();
        }

        store.Update(request.Name, request.Website);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Deletes the store together with its listings and their alerts.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await db.Stores.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? NotFound() : NoContent();
    }

    private static StoreResponse ToResponse(Store store) => new(store.Id, store.Name, store.Website);
}

using Microsoft.EntityFrameworkCore;
using StockAlerts.Core.Entities;
using StockAlerts.Core.StockChecks;

namespace StockAlerts.Infrastructure.Persistence;

internal sealed class StockCheckRepository(AppDbContext db) : IStockCheckRepository
{
    public async Task<IReadOnlyList<Listing>> GetListingsAsync(CancellationToken cancellationToken) =>
        await db.Listings.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetSubscriberIdsAsync(Guid productId, CancellationToken cancellationToken) =>
        await db.Subscriptions
            .Where(s => s.ProductId == productId)
            .Select(s => s.UserId)
            .ToListAsync(cancellationToken);

    public async Task<bool> TrySaveAsync(
        Listing listing, IReadOnlyCollection<Alert> newAlerts, CancellationToken cancellationToken)
    {
        db.Alerts.AddRange(newAlerts);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // SaveChanges runs in a transaction, so nothing was written. Stop tracking what we tried to
            // save; otherwise the next SaveChanges on this context would attempt it again.
            db.Entry(listing).State = EntityState.Detached;
            foreach (var alert in newAlerts)
            {
                db.Entry(alert).State = EntityState.Detached;
            }

            return false;
        }
    }
}

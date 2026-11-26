using CampusMarket.Api.Domain.Views;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfListingViewRepository(CampusMarketDbContext db) : IListingViewRepository
{
    // INSERT ... ON CONFLICT DO NOTHING on the unique index: two requests of the same viewer in the
    // same moment cannot make a duplicate or fail.
    public async Task<bool> TryAddAsync(ListingView view, CancellationToken ct = default) =>
        await db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "ListingViews" ("Id", "ListingId", "ViewerKey", "Day", "CreatedAt")
             VALUES ({view.Id}, {view.ListingId}, {view.ViewerKey}, {view.Day}, {view.CreatedAt})
             ON CONFLICT ("ListingId", "ViewerKey", "Day") DO NOTHING
             """, ct) > 0;

    public async Task<IReadOnlyList<DateOnly>> GetViewDaysAsync(Guid listingId, DateOnly from, CancellationToken ct = default) =>
        await db.ListingViews.AsNoTracking()
            .Where(v => v.ListingId == listingId && v.Day >= from)
            .Select(v => v.Day)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DateTimeOffset>> GetFavoriteTimesAsync(Guid listingId, DateTimeOffset from, CancellationToken ct = default) =>
        await db.Favorites.AsNoTracking()
            .Where(f => f.ListingId == listingId && f.CreatedAt >= from)
            .Select(f => f.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DateTimeOffset>> GetMessageTimesAsync(Guid listingId, DateTimeOffset from, CancellationToken ct = default) =>
        await db.Conversations.AsNoTracking()
            .Where(c => c.ListingId == listingId)
            .SelectMany(c => c.Messages)
            .Where(m => m.SentAt >= from)
            .Select(m => m.SentAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DateTimeOffset>> GetOfferTimesAsync(Guid listingId, DateTimeOffset from, CancellationToken ct = default) =>
        await db.Offers.AsNoTracking()
            .Where(o => o.ListingId == listingId && o.CreatedAt >= from)
            .Select(o => o.CreatedAt)
            .ToListAsync(ct);
}

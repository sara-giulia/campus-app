using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Search;

// Makes the missing vectors: every Active listing without a vector, or whose vector was made from
// another title/description or by another model. Only Active listings: the others are not searched,
// and a Reserved listing keeps its vector for when it is Active again. A deleted listing loses its
// vector with it (cascade).
public sealed partial class ListingIndexer(
    CampusMarketDbContext db, ITextEmbedder embedder, TimeProvider timeProvider, ILogger<ListingIndexer> logger)
{
    // Returns how many listings got a new vector.
    public async Task<int> IndexPendingAsync(CancellationToken ct)
    {
        var listings = await db.Listings.AsNoTracking()
            .Where(l => l.Status == ListingStatus.Active)
            .Select(l => new { l.Id, l.Title, l.Description })
            .ToListAsync(ct);
        var stored = await db.ListingEmbeddings.AsNoTracking()
            .Select(e => new { e.ListingId, e.Model, e.TextHash })
            .ToDictionaryAsync(e => e.ListingId, ct);

        var indexed = 0;
        foreach (var listing in listings)
        {
            var hash = ListingEmbedding.HashOf(listing.Title, listing.Description);
            if (stored.TryGetValue(listing.Id, out var current) && current.Model == embedder.ModelName && current.TextHash == hash)
                continue;

            var vector = embedder.EmbedListing(listing.Title, listing.Description);
            if (await SaveAsync(listing.Id, vector, hash, ct))
                indexed++;
        }
        return indexed;
    }

    // One listing per save: a listing deleted meanwhile only skips its own row.
    private async Task<bool> SaveAsync(Guid listingId, float[] vector, string hash, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var row = await db.ListingEmbeddings.FindAsync([listingId], ct);
        if (row is null)
            db.ListingEmbeddings.Add(ListingEmbedding.Create(listingId, vector, embedder.ModelName, hash, now));
        else
            row.Replace(vector, embedder.ModelName, hash, now);

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex)
        {
            LogSkipped(logger, listingId, ex);
            return false;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No vector saved for listing {ListingId} (deleted meanwhile?)")]
    private static partial void LogSkipped(ILogger logger, Guid listingId, Exception exception);
}

using CampusMarket.Api.Domain.Reviews;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfReviewRepository(CampusMarketDbContext db) : IReviewRepository
{
    public async Task<bool> AddAsync(Review review, CancellationToken ct = default)
    {
        if (await db.Reviews.AnyAsync(r => r.Id == review.Id, ct))
            return false;

        db.Reviews.Add(review);
        return true;
    }

    public Task<bool> ExistsForListingAsync(Guid listingId, CancellationToken ct = default) =>
        db.Reviews.AnyAsync(r => r.ListingId == listingId, ct);

    public async Task<IReadOnlyList<Review>> GetBySellerAsync(Guid sellerId, CancellationToken ct = default) =>
        await db.Reviews
            .AsNoTracking()
            .Where(r => r.SellerId == sellerId)
            .ToListAsync(ct);
}

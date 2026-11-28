using CampusMarket.Api.Domain.Offers;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfOfferRepository(CampusMarketDbContext db) : IOfferRepository
{
    public void Add(Offer offer) => db.Offers.Add(offer);

    public Task<Offer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Offers.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<bool> HasPendingAsync(Guid listingId, Guid buyerId, CancellationToken ct = default) =>
        db.Offers.AnyAsync(o => o.ListingId == listingId && o.BuyerId == buyerId && o.Status == OfferStatus.Pending, ct);

    public async Task<IReadOnlyList<Offer>> GetPendingForListingAsync(Guid listingId, CancellationToken ct = default) =>
        await db.Offers
            .Where(o => o.ListingId == listingId && o.Status == OfferStatus.Pending)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Offer>> GetForConversationAsync(Guid listingId, Guid buyerId, CancellationToken ct = default) =>
        await db.Offers
            .AsNoTracking()
            .Where(o => o.ListingId == listingId && o.BuyerId == buyerId)
            .OrderBy(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Offer>> GetPendingBetweenAsync(Guid memberId, Guid otherId, CancellationToken ct = default) =>
        await db.Offers
            .Where(o => o.Status == OfferStatus.Pending
                        && ((o.BuyerId == memberId && o.SellerId == otherId)
                            || (o.BuyerId == otherId && o.SellerId == memberId)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Offer>> GetPendingForMemberAsync(Guid memberId, CancellationToken ct = default) =>
        await db.Offers
            .Where(o => o.Status == OfferStatus.Pending && (o.BuyerId == memberId || o.SellerId == memberId))
            .ToListAsync(ct);
}

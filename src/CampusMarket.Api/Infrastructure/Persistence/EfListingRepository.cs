using CampusMarket.Api.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfListingRepository(CampusMarketDbContext db) : IListingRepository
{
    public async Task<bool> AddAsync(Listing listing, CancellationToken ct = default)
    {
        if (await db.Listings.AnyAsync(l => l.Id == listing.Id, ct))
            return false;

        db.Listings.Add(listing);
        return true;
    }

    public Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Listings
            .Include(l => l.Reservations)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<Listing>> GetAllAsync(CancellationToken ct = default) =>
        await db.Listings
            .AsNoTracking()
            .Include(l => l.Reservations)
            .ToListAsync(ct);

    public void Update(Listing listing) => db.Listings.Update(listing);

    public void Delete(Listing listing) => db.Listings.Remove(listing);
}

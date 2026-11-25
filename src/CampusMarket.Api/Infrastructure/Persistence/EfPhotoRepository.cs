using CampusMarket.Api.Domain.Photos;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfPhotoRepository(CampusMarketDbContext db) : IPhotoRepository
{
    public Task AddAsync(Photo photo, CancellationToken ct = default)
    {
        db.Photos.Add(photo);
        return Task.CompletedTask;
    }

    public Task<Photo?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Photos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Photo>> GetByListingAsync(Guid listingId, CancellationToken ct = default) =>
        await db.Photos.AsNoTracking().Where(p => p.ListingId == listingId).ToListAsync(ct);

    public Task<int> CountForListingAsync(Guid listingId, CancellationToken ct = default) =>
        db.Photos.CountAsync(p => p.ListingId == listingId, ct);

    public void Update(Photo photo) => db.Photos.Update(photo);

    public void Delete(Photo photo) => db.Photos.Remove(photo);
}

using CampusMarket.Api.Domain.Favorites;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfFavoriteRepository(CampusMarketDbContext db) : IFavoriteRepository
{
    public async Task<bool> AddAsync(Favorite favorite, CancellationToken ct = default)
    {
        if (await db.Favorites.AnyAsync(f => f.Id == favorite.Id, ct))
            return false;

        db.Favorites.Add(favorite);
        return true;
    }

    public Task<Favorite?> GetAsync(Guid memberId, Guid listingId, CancellationToken ct = default) =>
        db.Favorites.FirstOrDefaultAsync(f => f.MemberId == memberId && f.ListingId == listingId, ct);

    public async Task<IReadOnlyList<Favorite>> GetByMemberAsync(Guid memberId, CancellationToken ct = default) =>
        await db.Favorites
            .AsNoTracking()
            .Where(f => f.MemberId == memberId)
            .ToListAsync(ct);

    public void Delete(Favorite favorite) => db.Favorites.Remove(favorite);
}

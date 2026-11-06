using CampusMarket.Api.Domain.Favorites;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeFavoriteRepository : IFavoriteRepository
{
    public List<Favorite> Items { get; } = new();

    public bool RejectAdd { get; set; }

    public int DeleteCount { get; private set; }

    public Task<bool> AddAsync(Favorite favorite, CancellationToken ct = default)
    {
        if (RejectAdd)
            return Task.FromResult(false);

        if (Items.Any(f => f.Id == favorite.Id))
            return Task.FromResult(false);

        Items.Add(favorite);
        return Task.FromResult(true);
    }

    public Task<Favorite?> GetAsync(Guid memberId, Guid listingId, CancellationToken ct = default)
    {
        var favorite = Items.FirstOrDefault(f => f.MemberId == memberId && f.ListingId == listingId);
        return Task.FromResult(favorite);
    }

    public Task<IReadOnlyList<Favorite>> GetByMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        IReadOnlyList<Favorite> snapshot = Items.Where(f => f.MemberId == memberId).ToList();
        return Task.FromResult(snapshot);
    }

    public void Delete(Favorite favorite)
    {
        Items.Remove(favorite);
        DeleteCount++;
    }
}

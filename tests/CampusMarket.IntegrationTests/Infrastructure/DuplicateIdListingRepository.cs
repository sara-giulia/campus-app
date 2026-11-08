using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Infrastructure.Persistence;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Behaves like EfListingRepository, but AddAsync always answers that the id is already taken.
public sealed class DuplicateIdListingRepository(CampusMarketDbContext db) : IListingRepository
{
    private readonly EfListingRepository _inner = new(db);

    public Task<bool> AddAsync(Listing listing, CancellationToken ct = default) => Task.FromResult(false);

    public Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct = default) => _inner.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<Listing>> GetAllAsync(CancellationToken ct = default) => _inner.GetAllAsync(ct);

    public void Update(Listing listing) => _inner.Update(listing);

    public void Delete(Listing listing) => _inner.Delete(listing);
}

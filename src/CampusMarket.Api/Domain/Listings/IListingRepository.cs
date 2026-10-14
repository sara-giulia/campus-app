namespace CampusMarket.Api.Domain.Listings;

public interface IListingRepository
{
    Task AddAsync(Listing listing, CancellationToken ct = default);
    Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Listing>> GetAllAsync(CancellationToken ct = default);
    void Update(Listing listing);
    void Delete(Listing listing);
}
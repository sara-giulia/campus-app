namespace CampusMarket.Api.Domain.Listings;

public interface IListingRepository
{
    /// <summary>
    /// Adds a listing. Returns false if a listing with the same Id already exists
    /// (nothing is added). The endpoint maps false to 409 Conflict.
    /// </summary>
    Task<bool> AddAsync(Listing listing, CancellationToken ct = default);

    /// <summary>
    /// Returns the listing, or null if it does not exist (the endpoint answers 404).
    /// </summary>
    Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns a snapshot of all listings. Changing the returned list
    /// does not change what is stored.
    /// </summary>
    Task<IReadOnlyList<Listing>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the changes of a listing. Usage: call GetByIdAsync first, answer 404
    /// if it returns null, then call Update with the loaded listing.
    /// </summary>
    void Update(Listing listing);

    /// <summary>
    /// Removes a listing. Usage: call GetByIdAsync first, answer 404
    /// if it returns null, then call Delete with the loaded listing.
    /// </summary>
    void Delete(Listing listing);
}
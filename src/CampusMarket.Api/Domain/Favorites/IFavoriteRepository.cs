namespace CampusMarket.Api.Domain.Favorites;

public interface IFavoriteRepository
{
    /// <summary>
    /// Adds a favorite. Returns false if a favorite with the same Id already exists
    /// (nothing is added). The endpoint maps false to 409 Conflict.
    /// </summary>
    Task<bool> AddAsync(Favorite favorite, CancellationToken ct = default);

    /// <summary>
    /// Returns the member's favorite for this listing, or null if the member has not saved it.
    /// </summary>
    Task<Favorite?> GetAsync(Guid memberId, Guid listingId, CancellationToken ct = default);

    /// <summary>
    /// Returns a snapshot of all the member's favorites, in no particular order.
    /// </summary>
    Task<IReadOnlyList<Favorite>> GetByMemberAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Removes a favorite. Usage: call GetAsync first, answer 404
    /// if it returns null, then call Delete with the loaded favorite.
    /// </summary>
    void Delete(Favorite favorite);
}

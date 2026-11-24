using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Domain.Categories;

public interface ICategoryRepository
{
    /// <summary>
    /// Returns a snapshot of all categories, ordered by key. Changing the returned list
    /// does not change what is stored.
    /// </summary>
    Task<IReadOnlyList<ListingCategory>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the category, or null if it does not exist.
    /// </summary>
    Task<ListingCategory?> GetByKeyAsync(Category key, CancellationToken ct = default);

    /// <summary>
    /// Saves the changes of a category. Usage: load it with GetAllAsync or GetByKeyAsync first,
    /// then call Update with the loaded category.
    /// </summary>
    void Update(ListingCategory category);
}

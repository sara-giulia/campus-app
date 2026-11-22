using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfCategoryRepository(CampusMarketDbContext db) : ICategoryRepository
{
    // Sorted in memory: the key is stored as text, and the enum order is the order the API returns.
    public async Task<IReadOnlyList<ListingCategory>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .ToListAsync(ct);

        return categories.OrderBy(c => c.Key).ToList();
    }

    public Task<ListingCategory?> GetByKeyAsync(Category key, CancellationToken ct = default) =>
        db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == key, ct);

    public void Update(ListingCategory category) => db.Categories.Update(category);
}

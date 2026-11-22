using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeCategoryRepository : ICategoryRepository
{
    private static readonly Dictionary<Category, string> DisplayNames = new()
    {
        [Category.Textbook] = "Manuale",
        [Category.Notes] = "Notițe",
        [Category.Electronics] = "Electronice",
        [Category.LabEquipment] = "Echipament de laborator",
        [Category.Other] = "Altele"
    };

    public Dictionary<Category, ListingCategory> Items { get; } =
        DisplayNames.ToDictionary(p => p.Key, p => ListingCategory.Create(p.Key, p.Value).Value);

    public int UpdateCount { get; private set; }

    public void Deactivate(Category key)
    {
        Items[key] = ListingCategory.Create(key, DisplayNames[key], null, isActive: false).Value;
    }

    public Task<IReadOnlyList<ListingCategory>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ListingCategory> snapshot = Items.Values.OrderBy(c => c.Key).ToList();
        return Task.FromResult(snapshot);
    }

    public Task<ListingCategory?> GetByKeyAsync(Category key, CancellationToken ct = default)
    {
        Items.TryGetValue(key, out var category);
        return Task.FromResult(category);
    }

    public void Update(ListingCategory category)
    {
        Items[category.Key] = category;
        UpdateCount++;
    }
}

using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Categories.GetCategories;

public sealed record GetCategoriesResponse(IReadOnlyList<GetCategoriesItem> Items, int Count);

public sealed record GetCategoriesItem(Category Key, string DisplayName, string? Description, bool IsActive)
{
    public static GetCategoriesItem From(ListingCategory category) => new(
        category.Key,
        category.DisplayName,
        category.Description,
        category.IsActive);
}

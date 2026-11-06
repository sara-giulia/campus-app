using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Favorites.GetMyFavorites;

public sealed record GetMyFavoritesResponse(
    IReadOnlyList<MyFavoriteItem> Items, int Count, int TotalCount, int Page, int PageSize);

public sealed record MyFavoriteItem(
    Guid ListingId,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price,
    ListingStatus Status,
    DateTimeOffset FavoritedAt)
{
    public static MyFavoriteItem From(Favorite favorite, Listing listing) => new(
        listing.Id,
        listing.Title,
        listing.Category,
        listing.Condition,
        listing.Price.Amount,
        listing.Status,
        favorite.CreatedAt);
}

using CampusMarket.Api.Domain.Favorites;

namespace CampusMarket.Api.Features.Favorites.AddFavorite;

public sealed record AddFavoriteResponse(Guid Id, Guid ListingId, DateTimeOffset CreatedAt)
{
    public static AddFavoriteResponse From(Favorite favorite) => new(
        favorite.Id,
        favorite.ListingId,
        favorite.CreatedAt);
}

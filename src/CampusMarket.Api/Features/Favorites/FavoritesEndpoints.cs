using CampusMarket.Api.Features.Favorites.AddFavorite;
using CampusMarket.Api.Features.Favorites.GetMyFavorites;
using CampusMarket.Api.Features.Favorites.RemoveFavorite;

namespace CampusMarket.Api.Features.Favorites;

public static class FavoritesEndpoints
{
    public static IEndpointRouteBuilder MapFavoritesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/members/me/favorites");   // => /api/v1/members/me/favorites

        AddFavoriteEndpoint.Map(group);
        RemoveFavoriteEndpoint.Map(group);
        GetMyFavoritesEndpoint.Map(group);

        return app;
    }
}

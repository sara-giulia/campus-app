using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Favorites.GetMyFavorites;

public static class GetMyFavoritesEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetMyFavorites")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<GetMyFavoritesResponse>, BadRequest<Error>>> Handle(
        ClaimsPrincipal user,
        IQueryHandler<GetMyFavoritesQuery, GetMyFavoritesResponse> handler,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var query = new GetMyFavoritesQuery(
            user.GetMemberId(),
            page ?? GetMyFavoritesQuery.DefaultPage,
            pageSize ?? GetMyFavoritesQuery.DefaultPageSize);

        var result = await handler.HandleAsync(query, ct);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Error);
    }
}

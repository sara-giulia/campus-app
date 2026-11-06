using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Favorites.AddFavorite;

public static class AddFavoriteEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{listingId:guid}", Handle)
            .WithName("AddFavorite")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<AddFavoriteResponse>, NotFound<Error>, BadRequest<Error>, Conflict<Error>>> Handle(
        Guid listingId,
        ClaimsPrincipal user,
        ICommandHandler<AddFavoriteCommand, AddFavoriteResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new AddFavoriteCommand(user.GetMemberId(), listingId), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        // Location points to the member's favorites, where the new one is listed.
        return TypedResults.CreatedAtRoute(result.Value, "GetMyFavorites");
    }
}

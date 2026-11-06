using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Favorites.RemoveFavorite;

public static class RemoveFavoriteEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{listingId:guid}", Handle)
            .WithName("RemoveFavorite")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>>> Handle(
        Guid listingId,
        ClaimsPrincipal user,
        ICommandHandler<RemoveFavoriteCommand, Guid> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RemoveFavoriteCommand(user.GetMemberId(), listingId), ct);

        if (result.IsFailure)
            return TypedResults.NotFound(result.Error);

        return TypedResults.NoContent();
    }
}

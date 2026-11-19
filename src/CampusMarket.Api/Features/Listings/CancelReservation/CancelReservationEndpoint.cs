using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Listings.CancelReservation;

public static class CancelReservationEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}/reservations/{reservationId:guid}", Handle)
            .WithName("CancelReservation")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        Guid reservationId,
        ClaimsPrincipal user,
        ICommandHandler<CancelReservationCommand, Guid> handler,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var command = new CancelReservationCommand(id, reservationId, user.GetMemberId(), user.IsAdmin());

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        await ListingsCache.ClearAsync(cache, ct);

        return TypedResults.NoContent();
    }
}

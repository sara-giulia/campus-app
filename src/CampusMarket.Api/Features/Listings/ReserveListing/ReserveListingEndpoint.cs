using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.ReserveListing;

public static class ReserveListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/reservations", Handle)
            .WithName("ReserveListing")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<ReserveListingResponse>, NotFound<Error>, BadRequest<Error>>> Handle(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<ReserveListingCommand, ReserveListingResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ReserveListingCommand(id, user.GetMemberId()), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        // Location points to the buyer's reservations, where the new one is listed.
        return TypedResults.CreatedAtRoute(result.Value, "GetMyReservations");
    }
}

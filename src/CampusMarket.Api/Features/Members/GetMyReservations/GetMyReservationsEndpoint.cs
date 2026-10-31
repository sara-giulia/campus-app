using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.GetMyReservations;

public static class GetMyReservationsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/me/reservations", Handle)
            .WithName("GetMyReservations")
            .RequireAuthorization();
    }

    private static async Task<Ok<GetMyReservationsResponse>> Handle(
        ClaimsPrincipal user,
        IQueryHandler<GetMyReservationsQuery, GetMyReservationsResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetMyReservationsQuery(user.GetMemberId()), ct);

        return TypedResults.Ok(result.Value);
    }
}

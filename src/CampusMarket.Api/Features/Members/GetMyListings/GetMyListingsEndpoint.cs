using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.GetMyListings;

public static class GetMyListingsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/me/listings", Handle)
            .WithName("GetMyListings")
            .RequireAuthorization();
    }

    private static async Task<Ok<GetMyListingsResponse>> Handle(
        ClaimsPrincipal user,
        IQueryHandler<GetMyListingsQuery, GetMyListingsResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetMyListingsQuery(user.GetMemberId()), ct);

        return TypedResults.Ok(result.Value);
    }
}

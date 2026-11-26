using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.GetSimilarListings;

public static class GetSimilarListingsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/similar", Handle)
            .WithName("GetSimilarListings");
    }

    private static async Task<Results<Ok<GetSimilarListingsResponse>, NotFound<Error>>> Handle(
        [AsParameters] GetSimilarListingsRequest request,
        ClaimsPrincipal user,
        IQueryHandler<GetSimilarListingsQuery, GetSimilarListingsResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetSimilarListingsQuery(request.Id, user.GetMemberIdOrNull()), ct);

        if (result.IsFailure)
            return TypedResults.NotFound(result.Error);

        return TypedResults.Ok(result.Value);
    }
}

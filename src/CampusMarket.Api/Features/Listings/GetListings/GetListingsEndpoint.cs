using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.GetListings;

public static class GetListingsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetListings");
    }

    private static async Task<Ok<GetListingsResponse>> Handle(
        IQueryHandler<GetListingsQuery, GetListingsResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetListingsQuery(), ct);

        return TypedResults.Ok(result.Value);
    }
}

using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.GetListingById;

public static class GetListingByIdEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", Handle)
            .WithName("GetListingById");
    }

    private static async Task<Results<Ok<GetListingByIdResponse>, NotFound<Error>>> Handle(
        [AsParameters] GetListingByIdRequest request,
        IQueryHandler<GetListingByIdQuery, GetListingByIdResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetListingByIdQuery(request.Id), ct);

        if (result.IsFailure)
            return TypedResults.NotFound(result.Error);

        return TypedResults.Ok(result.Value);
    }
}

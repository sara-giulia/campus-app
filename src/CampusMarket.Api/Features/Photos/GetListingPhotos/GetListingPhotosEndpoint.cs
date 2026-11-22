using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Photos.GetListingPhotos;

public static class GetListingPhotosEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{listingId:guid}/photos", Handle)
            .WithName("GetListingPhotos");
    }

    private static async Task<Results<Ok<GetListingPhotosResponse>, NotFound<Error>>> Handle(
        Guid listingId,
        IQueryHandler<GetListingPhotosQuery, GetListingPhotosResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetListingPhotosQuery(listingId), ct);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.NotFound(result.Error);
    }
}

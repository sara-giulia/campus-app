namespace CampusMarket.Api.Features.Listings.GetListingById;

using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using Microsoft.AspNetCore.Http.HttpResults;

public static class GetListingByIdEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", Handle)
            .WithName("GetListingById");
    }

    private static async Task<Results<Ok<GetListingByIdResponse>, NotFound<Error>>> Handle(
        [AsParameters] GetListingByIdRequest request,
        IListingRepository repository,
        CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(request.Id, ct);

        if (listing is null)
            return TypedResults.NotFound(
                Error.NotFound("Listing.NotFound", $"Listing '{request.Id}' was not found."));

        return TypedResults.Ok(GetListingByIdResponse.From(listing));
    }
}
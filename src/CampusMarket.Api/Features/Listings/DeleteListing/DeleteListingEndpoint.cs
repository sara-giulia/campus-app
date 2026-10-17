using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.DeleteListing;

public static class DeleteListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", Handle)
            .WithName("DeleteListing");
    }

    // No response record: a successful delete answers 204 No Content, with an empty body.
    private static async Task<Results<NoContent, NotFound<Error>>> Handle(
        [AsParameters] DeleteListingRequest request,
        IListingRepository repository,
        CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(request.Id, ct);

        if (listing is null)
            return TypedResults.NotFound(
                Error.NotFound("Listing.NotFound", $"Listing '{request.Id}' was not found."));

        repository.Delete(listing);

        return TypedResults.NoContent();
    }
}
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.UpdateListing;

public static class UpdateListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", Handle)
            .WithName("UpdateListing");
    }

    // No response record: a successful update answers 204 No Content, with an empty body.
    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>>> Handle(
        Guid id,
        UpdateListingRequest request,
        IListingRepository repository,
        CancellationToken ct)
    {
        // 1. Load it; 404 if it does not exist.
        var listing = await repository.GetByIdAsync(id, ct);

        if (listing is null)
            return TypedResults.NotFound(
                Error.NotFound("Listing.NotFound", $"Listing '{id}' was not found."));

        // 2. The domain checks the rules (sold listing, title, price, enums).
        var result = listing.UpdateDetails(
            request.Title,
            request.Category,
            request.Condition,
            request.Price);

        if (result.IsFailure)
            return TypedResults.BadRequest(result.Error);

        // 3. Save and answer 204.
        repository.Update(listing);

        return TypedResults.NoContent();
    }
}

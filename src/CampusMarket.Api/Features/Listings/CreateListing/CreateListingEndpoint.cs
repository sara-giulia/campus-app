using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.CreateListing;

public static class CreateListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", Handle)
            .WithName("CreateListing");
    }

    private static async Task<Results<CreatedAtRoute<CreateListingResponse>, BadRequest<Error>, Conflict<Error>>> Handle(
        CreateListingRequest request,
        IListingRepository repository,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        // 1. The domain checks all the rules.
        var result = Listing.Create(
            request.SellerId,
            request.Title,
            request.Category,
            request.Condition,
            request.Price,
            timeProvider.GetUtcNow());

        if (result.IsFailure)
            return TypedResults.BadRequest(result.Error);

        // 2. The repository rejects a duplicate Id.
        var listing = result.Value;
        var added = await repository.AddAsync(listing, ct);

        if (!added)
            return TypedResults.Conflict(
                Error.Conflict("Listing.Duplicate", $"A listing with id '{listing.Id}' already exists."));

        // 3. 201 Created, with a Location header pointing to GetListingById.
        return TypedResults.CreatedAtRoute(
            CreateListingResponse.From(listing),
            "GetListingById",
            new { id = listing.Id });
    }
}
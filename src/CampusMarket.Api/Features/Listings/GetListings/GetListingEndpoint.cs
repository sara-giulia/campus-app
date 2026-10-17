using CampusMarket.Api.Domain.Listings;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.GetListings;

public static class GetListingsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetListings");
    }

    // No request record: this slice has no input (filters and paging come in a later assignment).
    private static async Task<Ok<GetListingsResponse>> Handle(
        IListingRepository repository,
        CancellationToken ct)
    {
        var listings = await repository.GetAllAsync(ct);

        var items = listings
            .Select(GetListingsItem.From)
            .ToList();

        return TypedResults.Ok(new GetListingsResponse(items, items.Count));
    }
}
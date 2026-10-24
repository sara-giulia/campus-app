using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.GetListings;

public sealed class GetListingsHandler(IListingRepository repository)
    : IQueryHandler<GetListingsQuery, GetListingsResponse>
{
    public async Task<Result<GetListingsResponse>> HandleAsync(
        GetListingsQuery query, CancellationToken ct)
    {
        var listings = await repository.GetAllAsync(ct);

        var items = listings
            .Select(GetListingsItem.From)
            .ToList();

        return new GetListingsResponse(items, items.Count);
    }
}

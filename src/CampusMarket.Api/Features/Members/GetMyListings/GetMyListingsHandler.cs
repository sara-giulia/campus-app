using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Members.GetMyListings;

// All listings of the logged-in member, in every status (Draft, Active, Reserved, Sold, Hidden, Expired),
// newest first. The public search shows only Active listings, so it cannot be used for this page.
public sealed class GetMyListingsHandler(IListingRepository repository)
    : IQueryHandler<GetMyListingsQuery, GetMyListingsResponse>
{
    public async Task<Result<GetMyListingsResponse>> HandleAsync(
        GetMyListingsQuery query, CancellationToken ct)
    {
        var listings = await repository.GetAllAsync(ct);

        var items = listings
            .Where(l => l.SellerId == query.MemberId)
            .OrderByDescending(l => l.CreatedAt)
            .ThenBy(l => l.Id)
            .Select(MyListingItem.From)
            .ToList();

        return new GetMyListingsResponse(items, items.Count);
    }
}

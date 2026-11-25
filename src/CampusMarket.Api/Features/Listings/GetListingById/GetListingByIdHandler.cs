using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.GetListingById;

public sealed class GetListingByIdHandler(IListingRepository repository)
    : IQueryHandler<GetListingByIdQuery, GetListingByIdResponse>
{
    public async Task<Result<GetListingByIdResponse>> HandleAsync(
        GetListingByIdQuery query, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(query.Id, ct);

        if (listing is null || !listing.IsVisibleTo(query.RequesterId, query.IsAdmin))
            return Result<GetListingByIdResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{query.Id}' was not found."));

        return GetListingByIdResponse.From(listing);
    }
}

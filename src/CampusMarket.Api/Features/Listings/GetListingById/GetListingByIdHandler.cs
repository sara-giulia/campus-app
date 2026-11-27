using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Listings.GetListingById;

// The listing of a suspended member is "not found" for everyone but an Admin (it comes back when the suspension ends).
public sealed class GetListingByIdHandler(IListingRepository repository, IMemberRepository members, TimeProvider timeProvider)
    : IQueryHandler<GetListingByIdQuery, GetListingByIdResponse>
{
    public async Task<Result<GetListingByIdResponse>> HandleAsync(
        GetListingByIdQuery query, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(query.Id, ct);

        if (listing is null || !listing.IsVisibleTo(query.RequesterId, query.IsAdmin)
            || (!query.IsAdmin && await members.IsSuspendedAsync(listing.SellerId, timeProvider.GetUtcNow(), ct)))
            return Result<GetListingByIdResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{query.Id}' was not found."));

        return GetListingByIdResponse.From(listing);
    }
}

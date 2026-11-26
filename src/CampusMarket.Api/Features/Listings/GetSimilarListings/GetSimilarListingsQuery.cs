using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.GetSimilarListings;

public sealed record GetSimilarListingsQuery(Guid Id, Guid? ViewerId = null) : IQuery<GetSimilarListingsResponse>
{
    // How many similar listings are returned at most.
    public const int MaxResults = 5;
}

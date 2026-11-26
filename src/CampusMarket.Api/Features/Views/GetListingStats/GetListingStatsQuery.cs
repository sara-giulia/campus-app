using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Views.GetListingStats;

public sealed record GetListingStatsQuery(Guid ListingId, Guid RequesterId, bool IsAdmin, int Days)
    : IQuery<GetListingStatsResponse>;

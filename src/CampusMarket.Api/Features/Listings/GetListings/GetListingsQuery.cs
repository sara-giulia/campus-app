using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.GetListings;

public sealed record GetListingsQuery() : IQuery<GetListingsResponse>;

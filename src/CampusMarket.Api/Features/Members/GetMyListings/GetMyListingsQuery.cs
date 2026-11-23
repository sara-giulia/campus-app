using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.GetMyListings;

public sealed record GetMyListingsQuery(Guid MemberId) : IQuery<GetMyListingsResponse>;

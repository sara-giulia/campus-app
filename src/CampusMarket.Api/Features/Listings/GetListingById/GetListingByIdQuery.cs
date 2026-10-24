using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.GetListingById;

public sealed record GetListingByIdQuery(Guid Id) : IQuery<GetListingByIdResponse>;

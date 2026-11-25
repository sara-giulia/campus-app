using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.GetListingById;

// RequesterId is null for an anonymous caller. A draft is shown only to its seller or an Admin.
public sealed record GetListingByIdQuery(Guid Id, Guid? RequesterId = null, bool IsAdmin = false) : IQuery<GetListingByIdResponse>;

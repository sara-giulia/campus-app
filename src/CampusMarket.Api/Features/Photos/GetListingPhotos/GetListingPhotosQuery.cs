using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Photos.GetListingPhotos;

// RequesterId is null for an anonymous caller. The photos of a draft are shown only to its seller or an Admin.
public sealed record GetListingPhotosQuery(Guid ListingId, Guid? RequesterId = null, bool IsAdmin = false) : IQuery<GetListingPhotosResponse>;

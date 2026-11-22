using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Photos.GetListingPhotos;

public sealed record GetListingPhotosQuery(Guid ListingId) : IQuery<GetListingPhotosResponse>;

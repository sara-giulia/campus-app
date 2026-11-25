using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Features.Photos.GetListingPhotos;

namespace CampusMarket.Api.Features.Photos.ReorderPhotos;

// PhotoIds: every photo of the listing, exactly once, in the new order (the first one is the cover).
public sealed record ReorderPhotosCommand(Guid RequesterId, bool IsAdmin, Guid ListingId, IReadOnlyList<Guid> PhotoIds)
    : ICommand<GetListingPhotosResponse>;

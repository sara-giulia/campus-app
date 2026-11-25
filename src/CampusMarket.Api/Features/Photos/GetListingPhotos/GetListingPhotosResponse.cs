namespace CampusMarket.Api.Features.Photos.GetListingPhotos;

// The photos in their Position order; the first one is the cover.
public sealed record GetListingPhotosResponse(IReadOnlyList<PhotoResponse> Items, int Count);

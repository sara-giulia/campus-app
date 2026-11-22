namespace CampusMarket.Api.Features.Photos.GetListingPhotos;

// The photos in the order they were added.
public sealed record GetListingPhotosResponse(IReadOnlyList<PhotoResponse> Items, int Count);

namespace CampusMarket.Api.Features.Photos.ReorderPhotos;

/// <summary>
/// The JSON body of PUT /api/v1/listings/{listingId}/photos/order: the ids of all the photos of the listing,
/// in the wanted order.
/// </summary>
public sealed record ReorderPhotosRequest(IReadOnlyList<Guid>? PhotoIds);

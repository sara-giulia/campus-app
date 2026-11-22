using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Features.Photos;

// Url is where the browser loads the image from (GET /api/v1/photos/{id}).
public sealed record PhotoResponse(Guid Id, Guid ListingId, string Url, string ContentType, long SizeBytes, DateTimeOffset CreatedAt)
{
    public static PhotoResponse From(Photo photo) => new(
        photo.Id, photo.ListingId, $"/api/v1/photos/{photo.Id}", photo.ContentType, photo.SizeBytes, photo.CreatedAt);
}

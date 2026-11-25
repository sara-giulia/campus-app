using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Features.Photos;

// Url is where the browser loads the image from (GET /api/v1/photos/{id}). IsGenerated: made by the app
// for a listing without photos, so the page shows it as a generated image. Position 0 is the cover.
public sealed record PhotoResponse(
    Guid Id, Guid ListingId, string Url, string ContentType, long SizeBytes, DateTimeOffset CreatedAt, bool IsGenerated, int Position)
{
    public static PhotoResponse From(Photo photo) => new(
        photo.Id, photo.ListingId, $"/api/v1/photos/{photo.Id}", photo.ContentType, photo.SizeBytes, photo.CreatedAt,
        photo.IsGenerated, photo.Position);
}

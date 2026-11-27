using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Features.Photos;

// Url is where the browser loads the image from (GET /api/v1/photos/{id}); ThumbnailUrl is the small
// version for the cards and lists (GET /api/v1/photos/{id}/thumbnail), the same as Url for a photo
// without a thumbnail. IsGenerated: made by the app for a listing without photos, so the page shows it as
// a generated image. Position 0 is the cover.
public sealed record PhotoResponse(
    Guid Id, Guid ListingId, string Url, string ThumbnailUrl, string ContentType, long SizeBytes, DateTimeOffset CreatedAt,
    bool IsGenerated, int Position)
{
    public static PhotoResponse From(Photo photo)
    {
        var url = $"/api/v1/photos/{photo.Id}";
        return new PhotoResponse(
            photo.Id, photo.ListingId, url, photo.ThumbnailFileName is null ? url : $"{url}/thumbnail",
            photo.ContentType, photo.SizeBytes, photo.CreatedAt, photo.IsGenerated, photo.Position);
    }
}

using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Domain.Photos;

// A photo of a listing. The file itself is on disk (IPhotoStorage); the database keeps only its name,
// type and size. Only the seller adds photos, at most MaxPerListing, each at most MaxSizeBytes.
public sealed class Photo
{
    public const int MaxPerListing = 5;
    public const long MaxSizeBytes = 2 * 1024 * 1024;

    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Photo() { }

    // existingCount: how many photos the listing already has. The format is read from the first bytes of
    // the file (PhotoFormat), never from the name or the type the browser sent, which anyone can change.
    public static Result<Photo> Create(Guid uploaderId, Listing listing, int existingCount, byte[] content, DateTimeOffset createdAt)
    {
        if (uploaderId != listing.SellerId)
            return Result<Photo>.Failure(
                Error.Forbidden("Photo.NotSeller", "Only the seller can change the photos of this listing."));

        if (existingCount >= MaxPerListing)
            return Result<Photo>.Failure(
                Error.Validation("Photo.LimitReached", $"A listing can have at most {MaxPerListing} photos."));

        if (content.Length == 0)
            return Result<Photo>.Failure(
                Error.Validation("Photo.Empty", "No file was sent."));

        if (content.Length > MaxSizeBytes)
            return Result<Photo>.Failure(
                Error.Validation("Photo.TooLarge", $"A photo must be at most {MaxSizeBytes / (1024 * 1024)} MB."));

        var format = PhotoFormat.Detect(content);
        if (format is null)
            return Result<Photo>.Failure(
                Error.Validation("Photo.InvalidFormat", "Only JPEG, PNG and WebP images are allowed."));

        var id = Guid.NewGuid();
        return new Photo
        {
            Id = id,
            ListingId = listing.Id,
            FileName = $"{id}{format.Extension}",
            ContentType = format.ContentType,
            SizeBytes = content.Length,
            CreatedAt = createdAt
        };
    }
}

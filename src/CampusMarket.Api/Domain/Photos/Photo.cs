using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Domain.Photos;

// A photo of a listing. The file itself is on disk (IPhotoStorage); the database keeps only its name,
// type and size. Only the seller adds photos, at most MaxPerListing, each at most MaxSizeBytes.
// A generated photo (IsGenerated) is made by the app for a listing without photos, so its page is not
// empty; it goes away when the seller adds a real one.
public sealed class Photo
{
    public const int MaxPerListing = 5;
    public const long MaxSizeBytes = 2 * 1024 * 1024;

    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    // Stryker disable once String : the factory always sets FileName, so the default value is never observable
    public string FileName { get; private set; } = string.Empty;
    // Stryker disable once String : the factory always sets ContentType, so the default value is never observable
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsGenerated { get; private set; }
    // Order in the listing (0 = first = the cover). New photos go at the end; PUT .../photos/order changes it.
    public int Position { get; private set; }

    private Photo() { }

    // existingCount: how many photos the listing already has. The format is read from the first bytes of
    // the file (PhotoFormat), never from the name or the type the browser sent, which anyone can change.
    public static Result<Photo> Create(
        Guid uploaderId, Listing listing, int existingCount, byte[] content, DateTimeOffset createdAt, int position = 0)
    {
        if (uploaderId != listing.SellerId)
            return Result<Photo>.Failure(
                Error.Forbidden("Photo.NotSeller", "Only the seller can change the photos of this listing."));

        if (existingCount >= MaxPerListing)
            return Result<Photo>.Failure(
                Error.Validation("Photo.LimitReached", $"A listing can have at most {MaxPerListing} photos."));

        var sizeError = CheckSize(content);
        if (sizeError is not null)
            return Result<Photo>.Failure(sizeError);

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
            CreatedAt = createdAt,
            Position = position
        };
    }

    // Only an Active listing (a public page) with no photo at all gets a generated image. Checked before
    // the image is drawn, and again when it is saved.
    public static Error? CheckCanGenerate(Listing listing, int existingCount)
    {
        if (listing.Status != ListingStatus.Active)
            return Error.Validation("Photo.ListingNotActive", "Only an active listing gets a generated image.");

        if (existingCount > 0)
            return Error.Conflict("Photo.ListingHasPhotos", "The listing already has a photo.");

        return null;
    }

    // The image the app made for a listing that has no photo. The format comes from the generator
    // (SVG, or PNG/JPEG/WebP from a model), not from the bytes, because the app made the file itself.
    public static Result<Photo> CreateGenerated(
        Listing listing, int existingCount, byte[] content, PhotoFormat format, DateTimeOffset createdAt)
    {
        var listingError = CheckCanGenerate(listing, existingCount);
        if (listingError is not null)
            return Result<Photo>.Failure(listingError);

        var sizeError = CheckSize(content);
        if (sizeError is not null)
            return Result<Photo>.Failure(sizeError);

        var id = Guid.NewGuid();
        return new Photo
        {
            Id = id,
            ListingId = listing.Id,
            FileName = $"{id}{format.Extension}",
            ContentType = format.ContentType,
            SizeBytes = content.Length,
            CreatedAt = createdAt,
            IsGenerated = true
        };
    }

    public void MoveTo(int position) => Position = position;

    // The same size rules for an upload and for a generated image.
    private static Error? CheckSize(byte[] content)
    {
        if (content.Length == 0)
            return Error.Validation("Photo.Empty", "No file was sent.");

        if (content.Length > MaxSizeBytes)
            return Error.Validation("Photo.TooLarge", $"A photo must be at most {MaxSizeBytes / (1024 * 1024)} MB.");

        return null;
    }
}

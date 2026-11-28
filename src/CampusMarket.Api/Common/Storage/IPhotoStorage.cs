namespace CampusMarket.Api.Common.Storage;

// Where the photo files live. A file is found by its listing and its name ("<listingId>/<fileName>"), the
// same key on the local disk (LocalPhotoStorage) and in a bucket (an S3/R2 storage), so deleting a listing
// removes all its files at once. The database keeps only the file names. Every call is async, because a
// bucket is reached over the network.
public interface IPhotoStorage
{
    // contentType is kept by a bucket and sent back with the file; the disk does not need it.
    Task SaveAsync(Guid listingId, string fileName, byte[] content, string contentType, CancellationToken ct = default);

    // Null when the file is not there.
    Task<Stream?> OpenReadAsync(Guid listingId, string fileName, CancellationToken ct = default);

    // Nothing happens when the file is not there.
    Task DeleteAsync(Guid listingId, string fileName, CancellationToken ct = default);

    // Removes every file of the listing; nothing happens when it has none.
    Task DeleteListingAsync(Guid listingId, CancellationToken ct = default);
}

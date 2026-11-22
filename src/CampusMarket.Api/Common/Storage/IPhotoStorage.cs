namespace CampusMarket.Api.Common.Storage;

// Where the photo files live (a folder on disk, the Docker volume in production). One folder per listing,
// so deleting a listing removes all its files at once. The database keeps only the file names.
public interface IPhotoStorage
{
    Task SaveAsync(Guid listingId, string fileName, byte[] content, CancellationToken ct = default);

    // Null when the file is not there.
    Stream? OpenRead(Guid listingId, string fileName);

    // Nothing happens when the file is not there.
    void Delete(Guid listingId, string fileName);

    // Removes every file of the listing; nothing happens when it has none.
    void DeleteListing(Guid listingId);
}

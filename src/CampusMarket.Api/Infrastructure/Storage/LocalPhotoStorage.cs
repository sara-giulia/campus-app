using CampusMarket.Api.Common.Storage;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Storage;

// Photos on the local disk: <PhotosPath>/<listingId>/<photoId>.<ext>. The names come from Photo.Create
// (a new Guid and a known extension), never from the user, so they cannot point outside the folder.
public sealed class LocalPhotoStorage(IOptions<PhotoStorageOptions> options, IWebHostEnvironment environment) : IPhotoStorage
{
    // Path.Combine keeps an absolute PhotosPath as it is and puts a relative one under the API folder.
    private readonly string _root = Path.Combine(environment.ContentRootPath, options.Value.PhotosPath);

    public async Task SaveAsync(Guid listingId, string fileName, byte[] content, CancellationToken ct = default)
    {
        var folder = ListingFolder(listingId);
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, fileName), content, ct);
    }

    public Stream? OpenRead(Guid listingId, string fileName)
    {
        var path = Path.Combine(ListingFolder(listingId), fileName);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(Guid listingId, string fileName) =>
        File.Delete(Path.Combine(ListingFolder(listingId), fileName));

    public void DeleteListing(Guid listingId)
    {
        var folder = ListingFolder(listingId);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    private string ListingFolder(Guid listingId) => Path.Combine(_root, listingId.ToString());
}

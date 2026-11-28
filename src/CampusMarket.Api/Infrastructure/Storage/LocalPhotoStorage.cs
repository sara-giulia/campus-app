using CampusMarket.Api.Common.Storage;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Storage;

// Photos on the local disk: <PhotosPath>/<listingId>/<fileName>. The names come from Photo (a new Guid and
// a known extension), never from the user, so they cannot point outside the folder.
public sealed class LocalPhotoStorage(IOptions<PhotoStorageOptions> options, IWebHostEnvironment environment) : IPhotoStorage
{
    // Path.Combine keeps an absolute PhotosPath as it is and puts a relative one under the API folder.
    private readonly string _root = Path.Combine(environment.ContentRootPath, options.Value.PhotosPath);

    public async Task SaveAsync(Guid listingId, string fileName, byte[] content, string contentType, CancellationToken ct = default)
    {
        var folder = ListingFolder(listingId);
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, fileName), content, ct);
    }

    public Task<Stream?> OpenReadAsync(Guid listingId, string fileName, CancellationToken ct = default)
    {
        var path = Path.Combine(ListingFolder(listingId), fileName);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(Guid listingId, string fileName, CancellationToken ct = default)
    {
        File.Delete(Path.Combine(ListingFolder(listingId), fileName));
        return Task.CompletedTask;
    }

    public Task DeleteListingAsync(Guid listingId, CancellationToken ct = default)
    {
        var folder = ListingFolder(listingId);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        return Task.CompletedTask;
    }

    private string ListingFolder(Guid listingId) => Path.Combine(_root, listingId.ToString());
}

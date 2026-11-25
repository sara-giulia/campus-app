using CampusMarket.Api.Common.Storage;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Saving a file always fails, like a full or unreachable disk. The message must never reach the client.
public sealed class ThrowingPhotoStorage : IPhotoStorage
{
    public Task SaveAsync(Guid listingId, string fileName, byte[] content, CancellationToken ct = default) =>
        throw new IOException("secret: disk /var/photos is full");

    public Stream? OpenRead(Guid listingId, string fileName) => null;

    public void Delete(Guid listingId, string fileName)
    {
    }

    public void DeleteListing(Guid listingId)
    {
    }
}

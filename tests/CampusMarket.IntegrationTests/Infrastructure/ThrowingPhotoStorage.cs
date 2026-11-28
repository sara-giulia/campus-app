using CampusMarket.Api.Common.Storage;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Saving a file always fails, like a full or unreachable disk. The message must never reach the client.
public sealed class ThrowingPhotoStorage : IPhotoStorage
{
    public Task SaveAsync(Guid listingId, string fileName, byte[] content, string contentType, CancellationToken ct = default) =>
        throw new IOException("secret: disk /var/photos is full");

    public Task<Stream?> OpenReadAsync(Guid listingId, string fileName, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(null);

    public Task DeleteAsync(Guid listingId, string fileName, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeleteListingAsync(Guid listingId, CancellationToken ct = default) => Task.CompletedTask;
}

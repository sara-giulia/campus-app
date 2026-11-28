using CampusMarket.Api.Common.Storage;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakePhotoStorage : IPhotoStorage
{
    public Dictionary<(Guid ListingId, string FileName), byte[]> Files { get; } = new();

    public Dictionary<(Guid ListingId, string FileName), string> ContentTypes { get; } = new();

    public List<Guid> DeletedListings { get; } = new();

    // When set, SaveAsync throws it instead of saving.
    public Exception? ThrowOnSave { get; set; }

    public Task SaveAsync(Guid listingId, string fileName, byte[] content, string contentType, CancellationToken ct = default)
    {
        if (ThrowOnSave is not null)
            throw ThrowOnSave;

        Files[(listingId, fileName)] = content;
        ContentTypes[(listingId, fileName)] = contentType;
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(Guid listingId, string fileName, CancellationToken ct = default)
    {
        Stream? stream = Files.TryGetValue((listingId, fileName), out var bytes) ? new MemoryStream(bytes) : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(Guid listingId, string fileName, CancellationToken ct = default)
    {
        Files.Remove((listingId, fileName));
        return Task.CompletedTask;
    }

    public Task DeleteListingAsync(Guid listingId, CancellationToken ct = default)
    {
        foreach (var key in Files.Keys.Where(k => k.ListingId == listingId).ToList())
            Files.Remove(key);

        DeletedListings.Add(listingId);
        return Task.CompletedTask;
    }
}

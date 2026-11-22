using CampusMarket.Api.Common.Storage;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakePhotoStorage : IPhotoStorage
{
    public Dictionary<(Guid ListingId, string FileName), byte[]> Files { get; } = new();

    public List<Guid> DeletedListings { get; } = new();

    public Task SaveAsync(Guid listingId, string fileName, byte[] content, CancellationToken ct = default)
    {
        Files[(listingId, fileName)] = content;
        return Task.CompletedTask;
    }

    public Stream? OpenRead(Guid listingId, string fileName)
    {
        return Files.TryGetValue((listingId, fileName), out var bytes) ? new MemoryStream(bytes) : null;
    }

    public void Delete(Guid listingId, string fileName)
    {
        Files.Remove((listingId, fileName));
    }

    public void DeleteListing(Guid listingId)
    {
        foreach (var key in Files.Keys.Where(k => k.ListingId == listingId).ToList())
            Files.Remove(key);

        DeletedListings.Add(listingId);
    }
}

using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakePhotoRepository : IPhotoRepository
{
    public List<Photo> Items { get; } = new();

    public Task AddAsync(Photo photo, CancellationToken ct = default)
    {
        Items.Add(photo);
        return Task.CompletedTask;
    }

    public Task<Photo?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var photo = Items.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(photo);
    }

    public Task<IReadOnlyList<Photo>> GetByListingAsync(Guid listingId, CancellationToken ct = default)
    {
        IReadOnlyList<Photo> snapshot = Items.Where(p => p.ListingId == listingId).ToList();
        return Task.FromResult(snapshot);
    }

    public Task<int> CountForListingAsync(Guid listingId, CancellationToken ct = default)
    {
        return Task.FromResult(Items.Count(p => p.ListingId == listingId));
    }

    public void Delete(Photo photo)
    {
        Items.Remove(photo);
    }
}

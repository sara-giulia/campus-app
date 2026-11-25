using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeListingRepository : IListingRepository
{
    public Dictionary<Guid, Listing> Items { get; } = new();

    public bool RejectAdd { get; set; }

    public int AddCount { get; private set; }

    public int UpdateCount { get; private set; }

    public Task<bool> AddAsync(Listing listing, CancellationToken ct = default)
    {
        if (RejectAdd)
            return Task.FromResult(false);

        if (Items.ContainsKey(listing.Id))
            return Task.FromResult(false);

        Items[listing.Id] = listing;
        AddCount++;
        return Task.FromResult(true);
    }

    public Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        Items.TryGetValue(id, out var listing);
        return Task.FromResult(listing);
    }

    public Task<IReadOnlyList<Listing>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Listing> snapshot = Items.Values.ToList();
        return Task.FromResult(snapshot);
    }

    public void Update(Listing listing)
    {
        Items[listing.Id] = listing;
        UpdateCount++;
    }

    public void Delete(Listing listing)
    {
        Items.Remove(listing.Id);
    }
}

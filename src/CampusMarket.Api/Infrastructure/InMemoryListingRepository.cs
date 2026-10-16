using System.Collections.Concurrent;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Infrastructure;

/// <summary>
/// Keeps listings in memory. Registered as a singleton, so the data lives as long as the app.
/// </summary>
public sealed class InMemoryListingRepository : IListingRepository
{
    private readonly ConcurrentDictionary<Guid, Listing> _listings = new();

    public Task<bool> AddAsync(Listing listing, CancellationToken ct = default)
    {
        // TryAdd returns false if the Id already exists, and then nothing is added.
        var added = _listings.TryAdd(listing.Id, listing);
        return Task.FromResult(added);
    }

    public Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // TryGetValue does not crash when the Id is missing; listing stays null.
        _listings.TryGetValue(id, out var listing);
        return Task.FromResult<Listing?>(listing);
    }

    public Task<IReadOnlyList<Listing>> GetAllAsync(CancellationToken ct = default)
    {
        // ToList() makes a copy, so whoever receives the list cannot change the store.
        IReadOnlyList<Listing> snapshot = _listings.Values.ToList();
        return Task.FromResult(snapshot);
    }

    public void Update(Listing listing)
    {
        // Only replaces an existing listing; it never adds a new one by accident.
        if (_listings.TryGetValue(listing.Id, out var existing))
        {
            _listings.TryUpdate(listing.Id, listing, existing);
        }
    }

    public void Delete(Listing listing)
    {
        _listings.TryRemove(listing.Id, out _);
    }
}
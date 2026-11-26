using CampusMarket.Api.Domain.Views;

namespace CampusMarket.UnitTests.Fakes;

// Keeps the views it is given (one per listing, key and day, like the unique index) and answers the four
// getters with the lists a test puts in; it remembers the "from" it was asked for.
public sealed class FakeListingViewRepository : IListingViewRepository
{
    public List<ListingView> Items { get; } = new();
    public List<DateOnly> ViewDays { get; } = new();
    public List<DateTimeOffset> FavoriteTimes { get; } = new();
    public List<DateTimeOffset> MessageTimes { get; } = new();
    public List<DateTimeOffset> OfferTimes { get; } = new();

    public int TryAddCount { get; private set; }
    public DateOnly? ViewsFrom { get; private set; }
    public DateTimeOffset? FavoritesFrom { get; private set; }
    public DateTimeOffset? MessagesFrom { get; private set; }
    public DateTimeOffset? OffersFrom { get; private set; }

    public Task<bool> TryAddAsync(ListingView view, CancellationToken ct = default)
    {
        TryAddCount++;
        if (Items.Any(v => v.ListingId == view.ListingId && v.ViewerKey == view.ViewerKey && v.Day == view.Day))
            return Task.FromResult(false);

        Items.Add(view);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<DateOnly>> GetViewDaysAsync(Guid listingId, DateOnly from, CancellationToken ct = default)
    {
        ViewsFrom = from;
        return Task.FromResult<IReadOnlyList<DateOnly>>(ViewDays.ToList());
    }

    public Task<IReadOnlyList<DateTimeOffset>> GetFavoriteTimesAsync(Guid listingId, DateTimeOffset from, CancellationToken ct = default)
    {
        FavoritesFrom = from;
        return Task.FromResult<IReadOnlyList<DateTimeOffset>>(FavoriteTimes.ToList());
    }

    public Task<IReadOnlyList<DateTimeOffset>> GetMessageTimesAsync(Guid listingId, DateTimeOffset from, CancellationToken ct = default)
    {
        MessagesFrom = from;
        return Task.FromResult<IReadOnlyList<DateTimeOffset>>(MessageTimes.ToList());
    }

    public Task<IReadOnlyList<DateTimeOffset>> GetOfferTimesAsync(Guid listingId, DateTimeOffset from, CancellationToken ct = default)
    {
        OffersFrom = from;
        return Task.FromResult<IReadOnlyList<DateTimeOffset>>(OfferTimes.ToList());
    }
}

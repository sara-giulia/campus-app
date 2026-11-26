namespace CampusMarket.Api.Domain.Views;

public interface IListingViewRepository
{
    /// <summary>
    /// Saves the view right away unless the viewer already viewed the listing that day (safe when two
    /// requests arrive together). True when the view is new.
    /// </summary>
    Task<bool> TryAddAsync(ListingView view, CancellationToken ct = default);

    /// <summary>
    /// The unique views of the listing from this day on (the day of each view, one entry per view).
    /// </summary>
    Task<IReadOnlyList<DateOnly>> GetViewDaysAsync(Guid listingId, DateOnly from, CancellationToken ct = default);

    /// <summary>
    /// When each favorite of the listing was added, from this moment on.
    /// </summary>
    Task<IReadOnlyList<DateTimeOffset>> GetFavoriteTimesAsync(Guid listingId, DateTimeOffset from, CancellationToken ct = default);

    /// <summary>
    /// When each message of the listing's conversations was sent, from this moment on.
    /// </summary>
    Task<IReadOnlyList<DateTimeOffset>> GetMessageTimesAsync(Guid listingId, DateTimeOffset from, CancellationToken ct = default);

    /// <summary>
    /// When each offer on the listing was made (counter-offers included), from this moment on.
    /// </summary>
    Task<IReadOnlyList<DateTimeOffset>> GetOfferTimesAsync(Guid listingId, DateTimeOffset from, CancellationToken ct = default);
}

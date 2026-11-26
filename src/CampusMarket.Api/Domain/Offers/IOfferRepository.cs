namespace CampusMarket.Api.Domain.Offers;

public interface IOfferRepository
{
    /// <summary>
    /// Adds a new offer. Saved by IUnitOfWork.CommitAsync.
    /// </summary>
    void Add(Offer offer);

    /// <summary>
    /// The offer, or null. It is tracked, so changes to it are saved by IUnitOfWork.CommitAsync.
    /// </summary>
    Task<Offer?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// True when the buyer already has a Pending offer (from either side) on this listing.
    /// </summary>
    Task<bool> HasPendingAsync(Guid listingId, Guid buyerId, CancellationToken ct = default);

    /// <summary>
    /// Every Pending offer on the listing, all buyers, tracked (accepting one closes the others).
    /// </summary>
    Task<IReadOnlyList<Offer>> GetPendingForListingAsync(Guid listingId, CancellationToken ct = default);

    /// <summary>
    /// A snapshot of the offers between this buyer and the seller of this listing (their conversation),
    /// the oldest first.
    /// </summary>
    Task<IReadOnlyList<Offer>> GetForConversationAsync(Guid listingId, Guid buyerId, CancellationToken ct = default);
}

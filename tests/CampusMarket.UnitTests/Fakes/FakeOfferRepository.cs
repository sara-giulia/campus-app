using CampusMarket.Api.Domain.Offers;

namespace CampusMarket.UnitTests.Fakes;

// The queries filter on the stored status, like EfOfferRepository: the database still has the status
// the offer had when it was added, until IUnitOfWork.CommitAsync saves the changes made in memory
// (FakeUnitOfWork saves nothing). An offer accepted by a handler is therefore still "Pending" for
// GetPendingForListingAsync in the same request, as in production, where EF Core returns the same tracked
// (already Accepted) instance.
public sealed class FakeOfferRepository : IOfferRepository
{
    private readonly Dictionary<Guid, OfferStatus> _storedStatus = new();

    public List<Offer> Items { get; } = new();

    public void Add(Offer offer)
    {
        Items.Add(offer);
        _storedStatus[offer.Id] = offer.Status;
    }

    private OfferStatus StoredStatus(Offer offer) =>
        _storedStatus.TryGetValue(offer.Id, out var status) ? status : offer.Status;

    public Task<Offer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(o => o.Id == id));

    public Task<bool> HasPendingAsync(Guid listingId, Guid buyerId, CancellationToken ct = default) =>
        Task.FromResult(Items.Any(o => o.ListingId == listingId && o.BuyerId == buyerId && StoredStatus(o) == OfferStatus.Pending));

    public Task<IReadOnlyList<Offer>> GetPendingForListingAsync(Guid listingId, CancellationToken ct = default)
    {
        IReadOnlyList<Offer> pending = Items
            .Where(o => o.ListingId == listingId && StoredStatus(o) == OfferStatus.Pending)
            .ToList();
        return Task.FromResult(pending);
    }

    public Task<IReadOnlyList<Offer>> GetForConversationAsync(Guid listingId, Guid buyerId, CancellationToken ct = default)
    {
        IReadOnlyList<Offer> offers = Items
            .Where(o => o.ListingId == listingId && o.BuyerId == buyerId)
            .OrderBy(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .ToList();
        return Task.FromResult(offers);
    }

    public Task<IReadOnlyList<Offer>> GetPendingBetweenAsync(Guid memberId, Guid otherId, CancellationToken ct = default)
    {
        IReadOnlyList<Offer> offers = Items
            .Where(o => StoredStatus(o) == OfferStatus.Pending
                        && ((o.BuyerId == memberId && o.SellerId == otherId)
                            || (o.BuyerId == otherId && o.SellerId == memberId)))
            .ToList();
        return Task.FromResult(offers);
    }

    public Task<IReadOnlyList<Offer>> GetPendingForMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        IReadOnlyList<Offer> offers = Items
            .Where(o => StoredStatus(o) == OfferStatus.Pending
                        && (o.BuyerId == memberId || o.SellerId == memberId))
            .ToList();
        return Task.FromResult(offers);
    }
}

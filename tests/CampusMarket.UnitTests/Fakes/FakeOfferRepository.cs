using CampusMarket.Api.Domain.Offers;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeOfferRepository : IOfferRepository
{
    public List<Offer> Items { get; } = new();

    public void Add(Offer offer) => Items.Add(offer);

    public Task<Offer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(o => o.Id == id));

    public Task<bool> HasPendingAsync(Guid listingId, Guid buyerId, CancellationToken ct = default) =>
        Task.FromResult(Items.Any(o => o.ListingId == listingId && o.BuyerId == buyerId && o.Status == OfferStatus.Pending));

    public Task<IReadOnlyList<Offer>> GetPendingForListingAsync(Guid listingId, CancellationToken ct = default)
    {
        IReadOnlyList<Offer> pending = Items
            .Where(o => o.ListingId == listingId && o.Status == OfferStatus.Pending)
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
            .Where(o => o.Status == OfferStatus.Pending
                        && ((o.BuyerId == memberId && o.SellerId == otherId)
                            || (o.BuyerId == otherId && o.SellerId == memberId)))
            .ToList();
        return Task.FromResult(offers);
    }
}

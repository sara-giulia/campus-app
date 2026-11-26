using CampusMarket.Api.Domain.Reviews;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeReviewRepository : IReviewRepository
{
    public List<Review> Items { get; } = new();

    public bool RejectAdd { get; set; }

    public Task<bool> AddAsync(Review review, CancellationToken ct = default)
    {
        if (RejectAdd)
            return Task.FromResult(false);

        if (Items.Any(r => r.Id == review.Id))
            return Task.FromResult(false);

        Items.Add(review);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsForListingAsync(Guid listingId, CancellationToken ct = default)
    {
        return Task.FromResult(Items.Any(r => r.ListingId == listingId));
    }

    public Task<IReadOnlyList<Review>> GetBySellerAsync(Guid sellerId, CancellationToken ct = default)
    {
        IReadOnlyList<Review> snapshot = Items.Where(r => r.SellerId == sellerId).ToList();
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<Guid>> GetReviewedListingIdsAsync(Guid reviewerId, CancellationToken ct = default)
    {
        IReadOnlyList<Guid> snapshot = Items.Where(r => r.ReviewerId == reviewerId).Select(r => r.ListingId).ToList();
        return Task.FromResult(snapshot);
    }
}

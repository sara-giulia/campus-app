namespace CampusMarket.Api.Domain.Reviews;

public interface IReviewRepository
{
    /// <summary>
    /// Adds a review. Returns false if a review with the same Id already exists
    /// (nothing is added). The endpoint maps false to 409 Conflict.
    /// </summary>
    Task<bool> AddAsync(Review review, CancellationToken ct = default);

    /// <summary>
    /// True if the listing already has a review.
    /// </summary>
    Task<bool> ExistsForListingAsync(Guid listingId, CancellationToken ct = default);

    /// <summary>
    /// Returns a snapshot of all reviews of this seller, in no particular order.
    /// </summary>
    Task<IReadOnlyList<Review>> GetBySellerAsync(Guid sellerId, CancellationToken ct = default);
}

using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Domain.Reviews;

// After a sale is completed, the buyer rates the seller from 1 to 5, once per sale.
// A listing is sold at most once, so there is at most one review per listing (unique index in the database).
public sealed class Review
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxCommentLength = 500;

    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid SellerId { get; private set; }
    public Guid ReviewerId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Review() { }

    public static Result<Review> Create(Guid reviewerId, Listing listing, int rating, string? comment, DateTimeOffset createdAt)
    {
        if (reviewerId == Guid.Empty)
            return Result<Review>.Failure(
                Error.Validation("Review.ReviewerEmpty", "Reviewer id must not be empty."));

        if (listing.Status != ListingStatus.Sold)
            return Result<Review>.Failure(
                Error.Validation("Review.SaleNotCompleted", "Only a sold listing can be reviewed."));

        // A sold listing always has exactly one completed reservation (see Listing.ConfirmSale): its member is the buyer.
        var buyerId = listing.Reservations.First(r => r.Status == ReservationStatus.Completed).MemberId;
        if (reviewerId != buyerId)
            return Result<Review>.Failure(
                Error.Forbidden("Review.NotBuyer", "Only the buyer of this listing can review the seller."));

        if (rating < MinRating || rating > MaxRating)
            return Result<Review>.Failure(
                Error.Validation("Review.InvalidRating", $"Rating must be between {MinRating} and {MaxRating}."));

        if (comment is not null && comment.Trim().Length > MaxCommentLength)
            return Result<Review>.Failure(
                Error.Validation("Review.CommentTooLong", $"Comment must have at most {MaxCommentLength} characters."));

        return new Review
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            SellerId = listing.SellerId,
            ReviewerId = reviewerId,
            Rating = rating,
            // An empty or blank comment is stored as null, so "no comment" has only one form.
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAt = createdAt
        };
    }
}

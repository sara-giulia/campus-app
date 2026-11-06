using CampusMarket.Api.Domain.Reviews;

namespace CampusMarket.Api.Features.Reviews.AddReview;

public sealed record AddReviewResponse(
    Guid Id, Guid ListingId, Guid SellerId, int Rating, string? Comment, DateTimeOffset CreatedAt)
{
    public static AddReviewResponse From(Review review) => new(
        review.Id, review.ListingId, review.SellerId, review.Rating, review.Comment, review.CreatedAt);
}

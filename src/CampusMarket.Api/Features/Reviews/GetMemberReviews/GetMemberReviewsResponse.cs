using CampusMarket.Api.Domain.Reviews;

namespace CampusMarket.Api.Features.Reviews.GetMemberReviews;

// AverageRating is over all the reviews of the member (not only the page), with two decimals,
// and null when the member has no reviews yet. ReviewCount is the number of all their reviews.
public sealed record GetMemberReviewsResponse(
    Guid MemberId,
    double? AverageRating,
    int ReviewCount,
    IReadOnlyList<MemberReviewItem> Items,
    int Count,
    int Page,
    int PageSize);

public sealed record MemberReviewItem(
    Guid Id,
    Guid ListingId,
    Guid ReviewerId,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt)
{
    public static MemberReviewItem From(Review review) => new(
        review.Id,
        review.ListingId,
        review.ReviewerId,
        review.Rating,
        review.Comment,
        review.CreatedAt);
}

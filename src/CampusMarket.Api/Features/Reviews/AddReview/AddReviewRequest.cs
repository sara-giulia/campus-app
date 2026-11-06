namespace CampusMarket.Api.Features.Reviews.AddReview;

public sealed record AddReviewRequest(int Rating, string? Comment = null);

using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Reviews.AddReview;

// The reviewer comes from the token.
public sealed record AddReviewCommand(Guid ReviewerId, Guid ListingId, int Rating, string? Comment) : ICommand<AddReviewResponse>;

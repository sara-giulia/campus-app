using CampusMarket.Api.Features.Reviews.AddReview;
using CampusMarket.Api.Features.Reviews.GetMemberReviews;

namespace CampusMarket.Api.Features.Reviews;

public static class ReviewsEndpoints
{
    public static IEndpointRouteBuilder MapReviewsEndpoints(this IEndpointRouteBuilder app)
    {
        // The buyer reviews a sale: /api/v1/listings/{listingId}/reviews
        AddReviewEndpoint.Map(app.MapGroup("/listings"));

        // Everyone reads a member's reviews: /api/v1/members/{id}/reviews
        GetMemberReviewsEndpoint.Map(app.MapGroup("/members"));

        return app;
    }
}

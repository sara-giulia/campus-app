using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Reviews.AddReview;

public static class AddReviewEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{listingId:guid}/reviews", Handle)
            .WithName("AddReview")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<AddReviewResponse>, NotFound<Error>, BadRequest<Error>, Conflict<Error>, JsonHttpResult<Error>>> Handle(
        Guid listingId,
        AddReviewRequest request,
        ClaimsPrincipal user,
        ICommandHandler<AddReviewCommand, AddReviewResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(
            new AddReviewCommand(user.GetMemberId(), listingId, request.Rating, request.Comment), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        // Location points to the seller's reviews, where the new one is listed.
        return TypedResults.CreatedAtRoute(result.Value, "GetMemberReviews", new { id = result.Value.SellerId });
    }
}

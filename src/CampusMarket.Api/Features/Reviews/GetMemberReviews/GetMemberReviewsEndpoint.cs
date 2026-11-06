using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Reviews.GetMemberReviews;

public static class GetMemberReviewsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        // Public, like the other GET endpoints.
        group.MapGet("/{id:guid}/reviews", Handle)
            .WithName("GetMemberReviews");
    }

    private static async Task<Results<Ok<GetMemberReviewsResponse>, BadRequest<Error>, NotFound<Error>>> Handle(
        Guid id,
        IQueryHandler<GetMemberReviewsQuery, GetMemberReviewsResponse> handler,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var query = new GetMemberReviewsQuery(
            id,
            page ?? GetMemberReviewsQuery.DefaultPage,
            pageSize ?? GetMemberReviewsQuery.DefaultPageSize);

        var result = await handler.HandleAsync(query, ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.BadRequest(result.Error);

        return TypedResults.Ok(result.Value);
    }
}

using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.SuggestCategory;

public static class SuggestCategoryEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/category-suggestion", Handle)
            .WithName("SuggestCategory")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<SuggestCategoryResponse>, BadRequest<Error>>> Handle(
        SuggestCategoryRequest request,
        IQueryHandler<SuggestCategoryQuery, SuggestCategoryResponse> handler,
        CancellationToken ct)
    {
        var query = new SuggestCategoryQuery(request.Title, request.Description);

        var result = await handler.HandleAsync(query, ct);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Error);
    }
}

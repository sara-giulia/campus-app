using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Categories.GetCategories;

public static class GetCategoriesEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        // Public: the create and edit forms and the search filters need the list without a token.
        group.MapGet("/", Handle)
            .WithName("GetCategories");
    }

    private static async Task<Ok<GetCategoriesResponse>> Handle(
        IQueryHandler<GetCategoriesQuery, GetCategoriesResponse> handler,
        CancellationToken ct)
    {
        // The handler never fails: it returns all the categories, active or not.
        var result = await handler.HandleAsync(new GetCategoriesQuery(), ct);

        return TypedResults.Ok(result.Value);
    }
}

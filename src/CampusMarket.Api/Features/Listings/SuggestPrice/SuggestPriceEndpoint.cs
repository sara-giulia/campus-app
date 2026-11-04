using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.SuggestPrice;

public static class SuggestPriceEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/price-suggestion", Handle)
            .WithName("SuggestPrice")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<SuggestPriceResponse>, BadRequest<Error>>> Handle(
        SuggestPriceRequest request,
        IQueryHandler<SuggestPriceQuery, SuggestPriceResponse> handler,
        CancellationToken ct)
    {
        var query = new SuggestPriceQuery(request.Title, request.Category, request.Condition, request.Description);

        var result = await handler.HandleAsync(query, ct);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Error);
    }
}

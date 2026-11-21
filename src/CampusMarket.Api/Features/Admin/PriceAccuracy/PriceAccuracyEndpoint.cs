using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Admin.PriceAccuracy;

public static class PriceAccuracyEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/price-accuracy", Handle)
            .WithName("GetPriceAccuracy")
            .RequireAuthorization("Admin");
    }

    private static async Task<Ok<PriceAccuracyResponse>> Handle(
        IQueryHandler<PriceAccuracyQuery, PriceAccuracyResponse> handler,
        CancellationToken ct)
    {
        // The handler never fails: an empty database is a valid state (count 0, null metrics).
        var result = await handler.HandleAsync(new PriceAccuracyQuery(), ct);
        return TypedResults.Ok(result.Value);
    }
}

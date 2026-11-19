using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.GetListings;

public static class GetListingsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetListings")
            .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(30)).SetVaryByQuery("*").Tag(ListingsCache.Tag));
    }

    private static async Task<Results<Ok<GetListingsResponse>, BadRequest<Error>>> Handle(
        IQueryHandler<GetListingsQuery, GetListingsResponse> handler,
        Category? category,
        decimal? minPrice,
        decimal? maxPrice,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var query = new GetListingsQuery(
            category,
            minPrice,
            maxPrice,
            search,
            page ?? GetListingsQuery.DefaultPage,
            pageSize ?? GetListingsQuery.DefaultPageSize);

        var result = await handler.HandleAsync(query, ct);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Error);
    }
}
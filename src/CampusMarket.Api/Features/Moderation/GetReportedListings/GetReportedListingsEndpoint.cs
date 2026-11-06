using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Moderation.GetReportedListings;

public static class GetReportedListingsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetReportedListings")
            .RequireAuthorization("Admin");
    }

    private static async Task<Results<Ok<GetReportedListingsResponse>, BadRequest<Error>>> Handle(
        IQueryHandler<GetReportedListingsQuery, GetReportedListingsResponse> handler,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var query = new GetReportedListingsQuery(
            page ?? GetReportedListingsQuery.DefaultPage,
            pageSize ?? GetReportedListingsQuery.DefaultPageSize);

        var result = await handler.HandleAsync(query, ct);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Error);
    }
}

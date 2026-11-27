using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Moderation.GetReportedMembers;

public static class GetReportedMembersEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetReportedMembers")
            .RequireAuthorization("Admin");
    }

    private static async Task<Results<Ok<GetReportedMembersResponse>, BadRequest<Error>>> Handle(
        IQueryHandler<GetReportedMembersQuery, GetReportedMembersResponse> handler,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var query = new GetReportedMembersQuery(
            page ?? GetReportedMembersQuery.DefaultPage,
            pageSize ?? GetReportedMembersQuery.DefaultPageSize);

        var result = await handler.HandleAsync(query, ct);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Error);
    }
}

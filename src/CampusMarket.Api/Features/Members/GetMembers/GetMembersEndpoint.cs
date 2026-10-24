using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.GetMembers;

public static class GetMembersEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetMembers");
    }

    private static async Task<Ok<GetMembersResponse>> Handle(
        IQueryHandler<GetMembersQuery, GetMembersResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetMembersQuery(), ct);

        return TypedResults.Ok(result.Value);
    }
}

using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.GetPublicProfile;

public static class GetPublicProfileEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/profile", Handle)
            .WithName("GetPublicProfile");
    }

    private static async Task<Results<Ok<GetPublicProfileResponse>, NotFound<Error>>> Handle(
        Guid id,
        IQueryHandler<GetPublicProfileQuery, GetPublicProfileResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetPublicProfileQuery(id), ct);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.NotFound(result.Error);
    }
}
using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Views.GetListingStats;

public static class GetListingStatsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/stats", Handle)
            .WithName("GetListingStats")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<GetListingStatsResponse>, NotFound<Error>, BadRequest<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        ClaimsPrincipal user,
        IQueryHandler<GetListingStatsQuery, GetListingStatsResponse> handler,
        CancellationToken ct,
        int days = GetListingStatsHandler.DefaultDays)
    {
        var result = await handler.HandleAsync(new GetListingStatsQuery(id, user.GetMemberId(), user.IsAdmin(), days), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.Ok(result.Value);
    }
}

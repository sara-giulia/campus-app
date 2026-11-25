using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Blocks.GetMyBlocked;

public static class GetMyBlockedEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/me/blocked", Handle)
            .WithName("GetMyBlocked")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<GetMyBlockedResponse>, BadRequest<Error>>> Handle(
        ClaimsPrincipal user,
        IQueryHandler<GetMyBlockedQuery, GetMyBlockedResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetMyBlockedQuery(user.GetMemberId()), ct);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Error);
    }
}

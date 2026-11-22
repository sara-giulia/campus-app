using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Notifications.GetMyNotifications;

public static class GetMyNotificationsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetMyNotifications")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<GetMyNotificationsResponse>, BadRequest<Error>>> Handle(
        ClaimsPrincipal user,
        IQueryHandler<GetMyNotificationsQuery, GetMyNotificationsResponse> handler,
        bool? unreadOnly,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var query = new GetMyNotificationsQuery(
            user.GetMemberId(),
            unreadOnly ?? false,
            page ?? GetMyNotificationsQuery.DefaultPage,
            pageSize ?? GetMyNotificationsQuery.DefaultPageSize);

        var result = await handler.HandleAsync(query, ct);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Error);
    }
}

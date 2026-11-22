using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Notifications.MarkAllNotificationsRead;

public static class MarkAllNotificationsReadEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/read-all", Handle)
            .WithName("MarkAllNotificationsRead")
            .RequireAuthorization();
    }

    private static async Task<NoContent> Handle(
        ClaimsPrincipal user,
        ICommandHandler<MarkAllNotificationsReadCommand, int> handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(new MarkAllNotificationsReadCommand(user.GetMemberId()), ct);

        return TypedResults.NoContent();
    }
}

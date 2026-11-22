using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Notifications.MarkNotificationRead;

public static class MarkNotificationReadEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/read", Handle)
            .WithName("MarkNotificationRead")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>>> Handle(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<MarkNotificationReadCommand, Guid> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new MarkNotificationReadCommand(id, user.GetMemberId()), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.NoContent();
    }
}

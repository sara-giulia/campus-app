using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Conversations.MarkConversationRead;

public static class MarkConversationReadEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/read", Handle)
            .WithName("MarkConversationRead")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<MarkConversationReadCommand, int> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new MarkConversationReadCommand(user.GetMemberId(), id), ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden);

        return TypedResults.NoContent();
    }
}

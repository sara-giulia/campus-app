using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Conversations.SendMessage;

public static class SendMessageEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/messages", Handle)
            .WithName("SendMessage")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<MessageResponse>, NotFound<Error>, BadRequest<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        SendMessageRequest request,
        ClaimsPrincipal user,
        ICommandHandler<SendMessageCommand, MessageResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new SendMessageCommand(user.GetMemberId(), id, request.Text), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.CreatedAtRoute(result.Value, "GetConversation", new { id = result.Value.ConversationId });
    }
}

using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Conversations.StartConversation;

public static class StartConversationEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{listingId:guid}/conversations", Handle)
            .WithName("StartConversation")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<MessageResponse>, NotFound<Error>, BadRequest<Error>>> Handle(
        Guid listingId,
        SendMessageRequest request,
        ClaimsPrincipal user,
        ICommandHandler<StartConversationCommand, MessageResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(
            new StartConversationCommand(user.GetMemberId(), listingId, request.Text), ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.BadRequest(result.Error);

        // Location points to the conversation, where the new message is listed.
        return TypedResults.CreatedAtRoute(result.Value, "GetConversation", new { id = result.Value.ConversationId });
    }
}

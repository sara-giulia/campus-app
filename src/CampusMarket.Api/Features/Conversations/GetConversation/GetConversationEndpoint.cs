using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Conversations.GetConversation;

public static class GetConversationEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", Handle)
            .WithName("GetConversation")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<GetConversationResponse>, NotFound<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        ClaimsPrincipal user,
        IQueryHandler<GetConversationQuery, GetConversationResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetConversationQuery(user.GetMemberId(), id), ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden);

        return TypedResults.Ok(result.Value);
    }
}

using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Conversations.GetUnreadMessages;

public static class GetUnreadMessagesEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/me/conversations/unread", Handle)
            .WithName("GetUnreadMessages")
            .RequireAuthorization();
    }

    private static async Task<Ok<GetUnreadMessagesResponse>> Handle(
        ClaimsPrincipal user,
        IQueryHandler<GetUnreadMessagesQuery, GetUnreadMessagesResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetUnreadMessagesQuery(user.GetMemberId()), ct);

        return TypedResults.Ok(result.Value);
    }
}

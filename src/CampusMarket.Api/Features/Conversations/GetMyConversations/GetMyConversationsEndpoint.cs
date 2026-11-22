using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Conversations.GetMyConversations;

public static class GetMyConversationsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/me/conversations", Handle)
            .WithName("GetMyConversations")
            .RequireAuthorization();
    }

    private static async Task<Ok<GetMyConversationsResponse>> Handle(
        ClaimsPrincipal user,
        IQueryHandler<GetMyConversationsQuery, GetMyConversationsResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetMyConversationsQuery(user.GetMemberId()), ct);

        return TypedResults.Ok(result.Value);
    }
}

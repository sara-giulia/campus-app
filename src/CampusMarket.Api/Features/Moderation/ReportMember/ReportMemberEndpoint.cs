using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Moderation.ReportMember;

public static class ReportMemberEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/reports", Handle)
            .WithName("ReportMember")
            .RequireAuthorization();
    }

    private static async Task<Results<Created<ReportMemberResponse>, NotFound<Error>, BadRequest<Error>, Conflict<Error>>> Handle(
        Guid id,
        ReportMemberRequest request,
        ClaimsPrincipal user,
        ICommandHandler<ReportMemberCommand, ReportMemberResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(
            new ReportMemberCommand(user.GetMemberId(), id, request.Reason, request.ConversationId), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        // No Location: a member cannot read reports back, only Admins see them (grouped per member).
        return TypedResults.Created((string?)null, result.Value);
    }
}

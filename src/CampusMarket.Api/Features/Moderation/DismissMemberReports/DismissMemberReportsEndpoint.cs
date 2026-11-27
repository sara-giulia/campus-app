using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Moderation.DismissMemberReports;

public static class DismissMemberReportsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{memberId:guid}/dismiss-reports", Handle)
            .WithName("DismissMemberReports")
            .RequireAuthorization("Admin");
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>>> Handle(
        Guid memberId,
        ClaimsPrincipal user,
        ICommandHandler<DismissMemberReportsCommand, Guid> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new DismissMemberReportsCommand(memberId, user.GetMemberId()), ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.BadRequest(result.Error);

        return TypedResults.NoContent();
    }
}

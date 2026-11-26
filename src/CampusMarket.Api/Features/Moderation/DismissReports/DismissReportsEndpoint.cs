using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Moderation.DismissReports;

public static class DismissReportsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{listingId:guid}/dismiss-reports", Handle)
            .WithName("DismissReports")
            .RequireAuthorization("Admin");
    }

    // The listing itself does not change, so the listings cache is not cleared.
    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>>> Handle(
        Guid listingId,
        ClaimsPrincipal user,
        ICommandHandler<DismissReportsCommand, Guid> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new DismissReportsCommand(listingId, user.GetMemberId()), ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.BadRequest(result.Error);

        return TypedResults.NoContent();
    }
}

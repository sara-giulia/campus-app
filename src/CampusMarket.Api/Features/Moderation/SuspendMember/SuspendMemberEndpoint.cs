using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Features.Listings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Moderation.SuspendMember;

public static class SuspendMemberEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{memberId:guid}/suspend", Handle)
            .WithName("SuspendMember")
            .RequireAuthorization("Admin");
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>, Conflict<Error>, JsonHttpResult<Error>>> Handle(
        Guid memberId,
        SuspendMemberRequest request,
        ClaimsPrincipal user,
        ICommandHandler<SuspendMemberCommand, Guid> handler,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(
            new SuspendMemberCommand(memberId, user.GetMemberId(), request.Reason, request.Until), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        // The member's listings leave the search at once.
        await ListingsCache.ClearAsync(cache, ct);

        return TypedResults.NoContent();
    }
}

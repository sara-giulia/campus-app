using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Features.Listings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Moderation.LiftSuspension;

public static class LiftSuspensionEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{memberId:guid}/lift-suspension", Handle)
            .WithName("LiftSuspension")
            .RequireAuthorization("Admin");
    }

    private static async Task<Results<NoContent, NotFound<Error>, Conflict<Error>>> Handle(
        Guid memberId,
        ICommandHandler<LiftSuspensionCommand, Guid> handler,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new LiftSuspensionCommand(memberId), ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.Conflict(result.Error);

        // The member's listings come back in the search at once.
        await ListingsCache.ClearAsync(cache, ct);

        return TypedResults.NoContent();
    }
}

using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Features.Listings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Blocks.BlockMember;

public static class BlockMemberEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/block", Handle)
            .WithName("BlockMember")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>>> Handle(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<BlockMemberCommand, Guid> handler,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new BlockMemberCommand(user.GetMemberId(), id), ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.BadRequest(result.Error);

        // The search hides the blocked member's listings from now on.
        await ListingsCache.ClearAsync(cache, ct);

        return TypedResults.NoContent();
    }
}

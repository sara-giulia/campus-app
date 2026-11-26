using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Features.Listings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Blocks.UnblockMember;

public static class UnblockMemberEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}/block", Handle)
            .WithName("UnblockMember")
            .RequireAuthorization();
    }

    private static async Task<NoContent> Handle(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<UnblockMemberCommand, Guid> handler,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        await handler.HandleAsync(new UnblockMemberCommand(user.GetMemberId(), id), ct);

        // The unblocked member's listings show again in the search.
        await ListingsCache.ClearAsync(cache, ct);

        return TypedResults.NoContent();
    }
}

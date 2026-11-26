using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Listings.RenewListing;

public static class RenewListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/renew", Handle)
            .WithName("RenewListing")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<RenewListingCommand, Guid> handler,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RenewListingCommand(id, user.GetMemberId()), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        // The listing is back in the search.
        await ListingsCache.ClearAsync(cache, ct);

        return TypedResults.NoContent();
    }
}

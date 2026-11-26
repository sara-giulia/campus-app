using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Features.Listings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Offers.AcceptOffer;

public static class AcceptOfferEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/accept", Handle)
            .WithName("AcceptOffer")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<AcceptOfferResponse>, NotFound<Error>, BadRequest<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<AcceptOfferCommand, AcceptOfferResponse> handler,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new AcceptOfferCommand(user.GetMemberId(), id), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        // The listing is Reserved now, so the cached listing pages are stale.
        await ListingsCache.ClearAsync(cache, ct);

        return TypedResults.Ok(result.Value);
    }
}

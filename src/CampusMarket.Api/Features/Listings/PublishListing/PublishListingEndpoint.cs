using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Listings.PublishListing;

public static class PublishListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/publish", Handle)
            .WithName("PublishListing")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<PublishListingCommand, Guid> handler,
        IOutputCacheStore cache,
        IImageGenerationQueue imageQueue,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(
            new PublishListingCommand(id, user.GetMemberId(), user.IsAdmin()), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        await ListingsCache.ClearAsync(cache, ct);

        // A listing without photos gets a generated image in the background; publishing does not wait.
        imageQueue.Enqueue(id);

        return TypedResults.NoContent();
    }
}

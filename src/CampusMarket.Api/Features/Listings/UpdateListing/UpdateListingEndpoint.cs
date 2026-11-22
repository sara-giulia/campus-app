using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Listings.UpdateListing;

public static class UpdateListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", Handle)
            .WithName("UpdateListing")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        UpdateListingRequest request,
        ClaimsPrincipal user,
        ICommandHandler<UpdateListingCommand, Guid> handler,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var command = new UpdateListingCommand(
            id, user.GetMemberId(), user.IsAdmin(),
            request.Title, request.Category, request.Condition, request.Price, request.Description);

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        await ListingsCache.ClearAsync(cache, ct);

        return TypedResults.NoContent();
    }
}

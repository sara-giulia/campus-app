using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Listings.DeleteListing;

public static class DeleteListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", Handle)
            .WithName("DeleteListing")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, JsonHttpResult<Error>>> Handle(
        [AsParameters] DeleteListingRequest request,
        ClaimsPrincipal user,
        ICommandHandler<DeleteListingCommand, Guid> handler,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new DeleteListingCommand(request.Id, user.GetMemberId(), user.IsAdmin()), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.NotFound(result.Error)
            };

        await ListingsCache.ClearAsync(cache, ct);

        return TypedResults.NoContent();
    }
}

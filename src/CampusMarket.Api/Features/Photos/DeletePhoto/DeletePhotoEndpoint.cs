using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Photos.DeletePhoto;

public static class DeletePhotoEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{listingId:guid}/photos/{photoId:guid}", Handle)
            .WithName("DeletePhoto")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, JsonHttpResult<Error>>> Handle(
        Guid listingId,
        Guid photoId,
        ClaimsPrincipal user,
        ICommandHandler<DeletePhotoCommand, Guid> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new DeletePhotoCommand(user.GetMemberId(), listingId, photoId), ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden);

        return TypedResults.NoContent();
    }
}

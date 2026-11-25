using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Features.Photos.GetListingPhotos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Photos.ReorderPhotos;

public static class ReorderPhotosEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{listingId:guid}/photos/order", Handle)
            .WithName("ReorderPhotos")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<GetListingPhotosResponse>, NotFound<Error>, BadRequest<Error>, JsonHttpResult<Error>>> Handle(
        Guid listingId,
        ReorderPhotosRequest request,
        ClaimsPrincipal user,
        ICommandHandler<ReorderPhotosCommand, GetListingPhotosResponse> handler,
        CancellationToken ct)
    {
        var command = new ReorderPhotosCommand(user.GetMemberId(), user.IsAdmin(), listingId, request.PhotoIds ?? []);
        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.Ok(result.Value);
    }
}

using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.DeleteListing;

public static class DeleteListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", Handle)
            .WithName("DeleteListing");
    }

    private static async Task<Results<NoContent, NotFound<Error>, JsonHttpResult<Error>>> Handle(
        [AsParameters] DeleteListingRequest request,
        ICommandHandler<DeleteListingCommand, Guid> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new DeleteListingCommand(request.Id), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.NotFound(result.Error)
            };

        return TypedResults.NoContent();
    }
}

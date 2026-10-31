using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.UpdateListing;

public static class UpdateListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", Handle)
            .WithName("UpdateListing");
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>>> Handle(
        Guid id,
        UpdateListingRequest request,
        ICommandHandler<UpdateListingCommand, Guid> handler,
        CancellationToken ct)
    {
        var command = new UpdateListingCommand(
            id, request.Title, request.Category, request.Condition, request.Price);

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.NoContent();
    }
}

using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.CreateListing;

public static class CreateListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", Handle)
            .WithName("CreateListing");
    }

    private static async Task<Results<CreatedAtRoute<CreateListingResponse>, BadRequest<Error>, Conflict<Error>>> Handle(
        CreateListingRequest request,
        ICommandHandler<CreateListingCommand, CreateListingResponse> handler,
        CancellationToken ct)
    {
        var command = new CreateListingCommand(
            request.SellerId, request.Title, request.Category, request.Condition, request.Price);

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.CreatedAtRoute(
            result.Value, "GetListingById", new { id = result.Value.Id });
    }
}

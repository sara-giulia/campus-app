using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Listings.CreateListing;

public static class CreateListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", Handle)
            .WithName("CreateListing")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<CreateListingResponse>, BadRequest<Error>, Conflict<Error>>> Handle(
        CreateListingRequest request,
        ClaimsPrincipal user,
        ICommandHandler<CreateListingCommand, CreateListingResponse> handler,
        IOutputCacheStore cache,
        CancellationToken ct)
    {
        var command = new CreateListingCommand(
            user.GetMemberId(), request.Title, request.Category, request.Condition, request.Price, request.Description);

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        await ListingsCache.ClearAsync(cache, ct);

        return TypedResults.CreatedAtRoute(
            result.Value, "GetListingById", new { id = result.Value.Id });
    }
}

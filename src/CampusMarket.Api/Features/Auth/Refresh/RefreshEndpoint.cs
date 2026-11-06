using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Auth.Refresh;

public static class RefreshEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/refresh", Handle)
            .WithName("Refresh");
    }

    private static async Task<Results<Ok<RefreshResponse>, JsonHttpResult<Error>>> Handle(
        RefreshRequest request,
        ICommandHandler<RefreshCommand, RefreshResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RefreshCommand(request.RefreshToken), ct);

        if (result.IsFailure)
            return TypedResults.Json(result.Error, statusCode: StatusCodes.Status401Unauthorized);

        return TypedResults.Ok(result.Value);
    }
}
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Auth.Logout;

public static class LogoutEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/logout", Handle)
            .WithName("Logout");
    }

    private static async Task<Results<NoContent, JsonHttpResult<Error>>> Handle(
        LogoutRequest request,
        ICommandHandler<LogoutCommand, Guid> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new LogoutCommand(request.RefreshToken), ct);

        if (result.IsFailure)
            return TypedResults.Json(result.Error, statusCode: StatusCodes.Status401Unauthorized);

        return TypedResults.NoContent();
    }
}
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/login", Handle)
            .WithName("Login");
    }

    private static async Task<Results<Ok<LoginResponse>, JsonHttpResult<Error>>> Handle(
        LoginRequest request,
        ICommandHandler<LoginCommand, LoginResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), ct);

        if (result.IsFailure)
            return TypedResults.Json(result.Error, statusCode: StatusCodes.Status401Unauthorized);

        return TypedResults.Ok(result.Value);
    }
}


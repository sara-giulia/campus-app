using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/login", Handle)
            .WithName("Login")
            .RequireRateLimiting("login");
    }

    private static async Task<Results<Ok<LoginResponse>, JsonHttpResult<Error>>> Handle(
        LoginRequest request,
        ICommandHandler<LoginCommand, LoginResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), ct);

        // A suspended account (right password) is 403 Auth.AccountSuspended; everything else is 401.
        if (result.IsFailure)
            return TypedResults.Json(result.Error, statusCode: result.Error.Type == ErrorType.Forbidden
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized);

        return TypedResults.Ok(result.Value);
    }
}


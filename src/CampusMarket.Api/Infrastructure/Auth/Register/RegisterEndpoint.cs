using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Auth.Register;

public static class RegisterEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/register", Handle)
            .WithName("Register");
    }

    private static async Task<Results<CreatedAtRoute<RegisterResponse>, BadRequest<Error>, Conflict<Error>>> Handle(
        RegisterRequest request,
        ICommandHandler<RegisterCommand, RegisterResponse> handler,
        CancellationToken ct)
    {
        var command = new RegisterCommand(request.Name, request.Email, request.Password);

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.CreatedAtRoute(
            result.Value, "GetMemberById", new { id = result.Value.Id });
    }
}


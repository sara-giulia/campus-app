using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Auth.ChangePassword;

public static class ChangePasswordEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/change-password", Handle)
            .WithName("ChangePassword")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>>> Handle(
        ChangePasswordRequest request,
        ClaimsPrincipal user,
        ICommandHandler<ChangePasswordCommand, Guid> handler,
        CancellationToken ct)
    {
        var command = new ChangePasswordCommand(
            user.GetMemberId(), request.CurrentPassword, request.NewPassword, request.ConfirmNewPassword);

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.BadRequest(result.Error);

        return TypedResults.NoContent();
    }
}
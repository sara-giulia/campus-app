using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Auth.ResetPassword;

public static class ResetPasswordEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/reset-password", Handle)
            .WithName("ResetPassword");
    }

    private static async Task<Results<NoContent, BadRequest<Error>>> Handle(
        ResetPasswordRequest request,
        ICommandHandler<ResetPasswordCommand, Guid> handler,
        CancellationToken ct)
    {
        var command = new ResetPasswordCommand(request.Token, request.NewPassword, request.ConfirmNewPassword);
        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return TypedResults.BadRequest(result.Error);

        return TypedResults.NoContent();
    }
}

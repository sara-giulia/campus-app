using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Auth.ForgotPassword;

public static class ForgotPasswordEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/forgot-password", Handle)
            .WithName("ForgotPassword")
            .RequireRateLimiting("login");
    }

    private static async Task<Accepted<ForgotPasswordResponse>> Handle(
        ForgotPasswordRequest request,
        ICommandHandler<ForgotPasswordCommand, ForgotPasswordResponse> handler,
        CancellationToken ct)
    {
        // The handler always succeeds (same body whether the email exists or not),
        // so we don't branch on Result.IsFailure here.
        var result = await handler.HandleAsync(new ForgotPasswordCommand(request.Email), ct);
        return TypedResults.Accepted((string?)null, result.Value);
    }
}

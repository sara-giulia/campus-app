namespace CampusMarket.Api.Features.Auth.ForgotPassword;

// The response is intentionally the same whether the email exists or not,
// so an attacker cannot use this endpoint to enumerate registered members.
public sealed record ForgotPasswordResponse(string Message)
{
    public static readonly ForgotPasswordResponse Default =
        new("If the email is registered, a reset link has been sent.");
}

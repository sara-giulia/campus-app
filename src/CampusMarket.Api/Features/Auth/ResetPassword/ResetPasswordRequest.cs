namespace CampusMarket.Api.Features.Auth.ResetPassword;

public sealed record ResetPasswordRequest(string Token, string NewPassword, string ConfirmNewPassword);

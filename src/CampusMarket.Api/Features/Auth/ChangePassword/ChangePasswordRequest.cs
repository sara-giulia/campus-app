namespace CampusMarket.Api.Features.Auth.ChangePassword;

/// <summary>
/// The JSON body of POST /api/v1/auth/change-password
/// </summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);
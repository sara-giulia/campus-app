namespace CampusMarket.Api.Features.Auth.Logout;

/// <summary>
/// The JSON body of POST /api/v1/auth/logout
/// </summary>
public sealed record LogoutRequest(string RefreshToken);
namespace CampusMarket.Api.Features.Auth.Login;

/// <summary>
/// The JSON body of POST /api/v1/auth/login
/// </summary>
public sealed record LoginRequest(string Email, string Password);
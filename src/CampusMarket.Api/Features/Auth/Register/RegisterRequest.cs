namespace CampusMarket.Api.Features.Auth.Register;

/// <summary>
/// The JSON body of POST /api/v1/auth/register
/// </summary>
public sealed record RegisterRequest(string Name, string Email, string Password, string ConfirmPassword);
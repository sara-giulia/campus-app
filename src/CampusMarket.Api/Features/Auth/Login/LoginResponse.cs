namespace CampusMarket.Api.Features.Auth.Login;

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, string RefreshToken);
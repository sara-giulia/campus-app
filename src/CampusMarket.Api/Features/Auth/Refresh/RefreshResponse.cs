namespace CampusMarket.Api.Features.Auth.Refresh;

public sealed record RefreshResponse(string Token, DateTimeOffset ExpiresAt, string RefreshToken);
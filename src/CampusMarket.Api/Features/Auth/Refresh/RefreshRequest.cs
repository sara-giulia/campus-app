namespace CampusMarket.Api.Features.Auth.Refresh;

/// The JSON body of POST /api/v1/auth/refresh
public sealed record RefreshRequest(string RefreshToken);
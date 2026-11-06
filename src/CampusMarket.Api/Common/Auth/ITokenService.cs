using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Common.Auth;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(Member member);

    // A long random string (RandomNumberGenerator). Only its hash is stored, see RefreshToken.
    string CreateRefreshToken();
}
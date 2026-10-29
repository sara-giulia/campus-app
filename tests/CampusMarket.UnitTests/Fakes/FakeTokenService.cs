using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeTokenService : ITokenService
{
    public static readonly DateTimeOffset ExpiresAt = new(2026, 1, 1, 11, 0, 0, TimeSpan.Zero);

    public Member? LastMember { get; private set; }

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(Member member)
    {
        LastMember = member;
        return ($"token-{member.Id}", ExpiresAt);
    }
}

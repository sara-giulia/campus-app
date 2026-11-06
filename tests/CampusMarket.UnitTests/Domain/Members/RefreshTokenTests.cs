using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.UnitTests.Domain.Members;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid MemberId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Given_MemberAndToken_When_Creating_Then_StoresMemberTimesAndExpiryInSevenDays()
    {
        var refreshToken = RefreshToken.Create(MemberId, "refresh-1", Now);

        refreshToken.Id.Should().NotBe(Guid.Empty);
        refreshToken.MemberId.Should().Be(MemberId);
        refreshToken.CreatedAt.Should().Be(Now);
        refreshToken.ExpiresAt.Should().Be(Now.AddDays(7));
        refreshToken.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Given_MemberAndToken_When_Creating_Then_StoresOnlyTheHashOfTheToken()
    {
        var refreshToken = RefreshToken.Create(MemberId, "refresh-1", Now);

        refreshToken.TokenHash.Should().Be(RefreshToken.Hash("refresh-1"));
        refreshToken.TokenHash.Should().NotBe("refresh-1");
    }

    [Fact]
    public void Given_SameText_When_HashingTwice_Then_ReturnsTheSameHash()
    {
        var first = RefreshToken.Hash("refresh-1");

        var second = RefreshToken.Hash("refresh-1");

        second.Should().Be(first);
    }

    [Fact]
    public void Given_DifferentTexts_When_Hashing_Then_ReturnsDifferentHashes()
    {
        var first = RefreshToken.Hash("refresh-1");

        var second = RefreshToken.Hash("refresh-2");

        second.Should().NotBe(first);
    }

    [Fact]
    public void Given_Text_When_Hashing_Then_ReturnsSixtyFourUpperCaseHexCharacters()
    {
        var text = "refresh-1";

        var hash = RefreshToken.Hash(text);

        hash.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public void Given_NewToken_When_CheckingOneSecondBeforeExpiry_Then_ReturnsTrue()
    {
        var refreshToken = RefreshToken.Create(MemberId, "refresh-1", Now);

        var isActive = refreshToken.IsActive(refreshToken.ExpiresAt.AddSeconds(-1));

        isActive.Should().BeTrue();
    }

    [Fact]
    public void Given_NewToken_When_CheckingAtExpiry_Then_ReturnsFalse()
    {
        var refreshToken = RefreshToken.Create(MemberId, "refresh-1", Now);

        var isActive = refreshToken.IsActive(refreshToken.ExpiresAt);

        isActive.Should().BeFalse();
    }

    [Fact]
    public void Given_RevokedToken_When_CheckingIsActive_Then_ReturnsFalse()
    {
        var refreshToken = RefreshToken.Create(MemberId, "refresh-1", Now);
        refreshToken.Revoke(Now.AddHours(1));

        var isActive = refreshToken.IsActive(Now.AddHours(2));

        isActive.Should().BeFalse();
    }

    [Fact]
    public void Given_RevokedToken_When_RevokingAgain_Then_KeepsTheFirstRevokedAt()
    {
        var refreshToken = RefreshToken.Create(MemberId, "refresh-1", Now);
        refreshToken.Revoke(Now.AddHours(1));

        refreshToken.Revoke(Now.AddHours(2));

        refreshToken.RevokedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Given_InvalidError_When_Reading_Then_HasValidationTypeCodeAndMessage()
    {
        var error = RefreshToken.Invalid;

        error.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("Auth.InvalidRefreshToken");
        error.Message.Should().Be("The refresh token is not valid.");
    }
}

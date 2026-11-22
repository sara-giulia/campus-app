using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.UnitTests.Domain.Members;

public class PasswordResetTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_ValidData_When_Creating_Then_StoresHashNotPlainToken()
    {
        var memberId = Guid.NewGuid();

        var (token, plain) = PasswordResetToken.Create(memberId, Now);

        token.Id.Should().NotBe(Guid.Empty);
        token.MemberId.Should().Be(memberId);
        token.CreatedAt.Should().Be(Now);
        token.ExpiresAt.Should().Be(Now.Add(PasswordResetToken.Lifetime));
        token.UsedAt.Should().BeNull();
        plain.Should().NotBeNullOrEmpty();
        token.TokenHash.Should().NotBe(plain);
        token.TokenHash.Should().Be(PasswordResetToken.Hash(plain));
    }

    [Fact]
    public void Given_SamePlainToken_When_Hashing_Then_ReturnsSameHash()
    {
        var hashA = PasswordResetToken.Hash("plain-token");
        var hashB = PasswordResetToken.Hash("plain-token");

        hashA.Should().Be(hashB);
    }

    [Fact]
    public void Given_DifferentPlainTokens_When_Hashing_Then_ReturnsDifferentHashes()
    {
        var hashA = PasswordResetToken.Hash("plain-token-a");
        var hashB = PasswordResetToken.Hash("plain-token-b");

        hashA.Should().NotBe(hashB);
    }

    [Fact]
    public void Given_FreshToken_When_CheckingValidityBeforeExpiry_Then_ReturnsTrue()
    {
        var (token, _) = PasswordResetToken.Create(Guid.NewGuid(), Now);

        token.IsValid(Now.Add(PasswordResetToken.Lifetime).AddSeconds(-1)).Should().BeTrue();
    }

    [Fact]
    public void Given_FreshToken_When_CheckingValidityAtExpiry_Then_ReturnsFalse()
    {
        var (token, _) = PasswordResetToken.Create(Guid.NewGuid(), Now);

        token.IsValid(Now.Add(PasswordResetToken.Lifetime)).Should().BeFalse();
    }

    [Fact]
    public void Given_FreshToken_When_CheckingValidityAfterExpiry_Then_ReturnsFalse()
    {
        var (token, _) = PasswordResetToken.Create(Guid.NewGuid(), Now);

        token.IsValid(Now.Add(PasswordResetToken.Lifetime).AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void Given_UsedToken_When_CheckingValidity_Then_ReturnsFalse()
    {
        var (token, _) = PasswordResetToken.Create(Guid.NewGuid(), Now);
        token.MarkUsed(Now);

        token.IsValid(Now.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void Given_Token_When_MarkingUsedTwice_Then_KeepsTheFirstTime()
    {
        var (token, _) = PasswordResetToken.Create(Guid.NewGuid(), Now);
        token.MarkUsed(Now);

        token.MarkUsed(Now.AddMinutes(5));

        token.UsedAt.Should().Be(Now);
    }

    [Fact]
    public void Given_Invalid_When_Reading_Then_ReturnsExpectedError()
    {
        PasswordResetToken.Invalid.Code.Should().Be("Auth.InvalidResetToken");
        PasswordResetToken.Invalid.Message.Should().Be("The reset token is not valid.");
        PasswordResetToken.Invalid.Type.Should().Be(ErrorType.Validation);
    }
}

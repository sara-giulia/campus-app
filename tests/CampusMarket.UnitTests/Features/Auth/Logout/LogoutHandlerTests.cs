using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Logout;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Auth.Logout;

public class LogoutHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid MemberId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string Token = "refresh-token";

    [Fact]
    public async Task Given_ActiveToken_When_Handling_Then_RevokesTokenAndReturnsItsId()
    {
        var refreshTokens = new FakeRefreshTokenRepository();
        var stored = RefreshToken.Create(MemberId, Token, Now.AddHours(-1));
        refreshTokens.Items[stored.Id] = stored;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new LogoutHandler(refreshTokens, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new LogoutCommand(Token), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(stored.Id);
        stored.RevokedAt.Should().Be(Now);
        refreshTokens.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Given_EmptyToken_When_Handling_Then_ReturnsInvalidAndDoesNotSave(string token)
    {
        var unitOfWork = new FakeUnitOfWork();
        var handler = new LogoutHandler(new FakeRefreshTokenRepository(), unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new LogoutCommand(token), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        result.Error.Message.Should().Be("The refresh token is not valid.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownToken_When_Handling_Then_ReturnsInvalidAndDoesNotSave()
    {
        var refreshTokens = new FakeRefreshTokenRepository();
        var stored = RefreshToken.Create(MemberId, Token, Now.AddHours(-1));
        refreshTokens.Items[stored.Id] = stored;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new LogoutHandler(refreshTokens, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new LogoutCommand("unknown-token"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        result.Error.Message.Should().Be("The refresh token is not valid.");
        stored.RevokedAt.Should().BeNull();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AlreadyRevokedToken_When_Handling_Then_ReturnsInvalidAndKeepsRevokedAt()
    {
        var refreshTokens = new FakeRefreshTokenRepository();
        var stored = RefreshToken.Create(MemberId, Token, Now.AddHours(-2));
        stored.Revoke(Now.AddHours(-1));
        refreshTokens.Items[stored.Id] = stored;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new LogoutHandler(refreshTokens, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new LogoutCommand(Token), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        result.Error.Message.Should().Be("The refresh token is not valid.");
        stored.RevokedAt.Should().Be(Now.AddHours(-1));
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ExpiredToken_When_Handling_Then_ReturnsInvalidAndDoesNotSave()
    {
        var refreshTokens = new FakeRefreshTokenRepository();
        var stored = RefreshToken.Create(MemberId, Token, Now.AddDays(-8));
        refreshTokens.Items[stored.Id] = stored;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new LogoutHandler(refreshTokens, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new LogoutCommand(Token), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        result.Error.Message.Should().Be("The refresh token is not valid.");
        unitOfWork.SaveCount.Should().Be(0);
    }
}

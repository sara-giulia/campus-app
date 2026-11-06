using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Refresh;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Auth.Refresh;

public class RefreshHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private const string OldToken = "old-refresh-token";

    private static Member BuildMember() =>
        Member.Create("Ada", "ada@ubb.ro", Now).Value;

    [Fact]
    public async Task Given_ActiveToken_When_Handling_Then_ReturnsNewAccessAndRefreshToken()
    {
        var member = BuildMember();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var old = RefreshToken.Create(member.Id, OldToken, Now.AddHours(-1));
        refreshTokens.Items[old.Id] = old;
        var handler = new RefreshHandler(refreshTokens, members, new FakeTokenService(), new FakeUnitOfWork(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be($"token-{member.Id}");
        result.Value.ExpiresAt.Should().Be(FakeTokenService.ExpiresAt);
        result.Value.RefreshToken.Should().Be("refresh-1");
    }

    [Fact]
    public async Task Given_ActiveToken_When_Handling_Then_RevokesOldTokenAndStoresNewActiveTokenAndSavesOnce()
    {
        var member = BuildMember();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var old = RefreshToken.Create(member.Id, OldToken, Now.AddHours(-1));
        refreshTokens.Items[old.Id] = old;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RefreshHandler(refreshTokens, members, new FakeTokenService(), unitOfWork, new FixedTimeProvider(Now));

        await handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        old.RevokedAt.Should().Be(Now);
        refreshTokens.Items.Should().HaveCount(2);
        var created = refreshTokens.Items.Values.Single(t => t.Id != old.Id);
        created.MemberId.Should().Be(member.Id);
        created.TokenHash.Should().Be(RefreshToken.Hash("refresh-1"));
        created.IsActive(Now).Should().BeTrue();
        refreshTokens.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Given_EmptyToken_When_Handling_Then_ReturnsInvalidAndDoesNotSave(string token)
    {
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RefreshHandler(new FakeRefreshTokenRepository(), new FakeMemberRepository(), new FakeTokenService(), unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new RefreshCommand(token), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        result.Error.Message.Should().Be("The refresh token is not valid.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownToken_When_Handling_Then_ReturnsInvalidAndLeavesRepositoryUnchanged()
    {
        var member = BuildMember();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var old = RefreshToken.Create(member.Id, OldToken, Now.AddHours(-1));
        refreshTokens.Items[old.Id] = old;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RefreshHandler(refreshTokens, members, new FakeTokenService(), unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new RefreshCommand("unknown-token"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        result.Error.Message.Should().Be("The refresh token is not valid.");
        refreshTokens.Items.Should().ContainSingle().Which.Value.Should().BeSameAs(old);
        old.RevokedAt.Should().BeNull();
        refreshTokens.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_RevokedToken_When_Handling_Then_ReturnsInvalidAndStoresNoNewToken()
    {
        var member = BuildMember();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var old = RefreshToken.Create(member.Id, OldToken, Now.AddHours(-2));
        old.Revoke(Now.AddHours(-1));
        refreshTokens.Items[old.Id] = old;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RefreshHandler(refreshTokens, members, new FakeTokenService(), unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        result.Error.Message.Should().Be("The refresh token is not valid.");
        refreshTokens.Items.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TokenCreatedExactlySevenDaysAgo_When_Handling_Then_ReturnsInvalid()
    {
        var member = BuildMember();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var old = RefreshToken.Create(member.Id, OldToken, Now.AddDays(-7));
        refreshTokens.Items[old.Id] = old;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RefreshHandler(refreshTokens, members, new FakeTokenService(), unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        result.Error.Message.Should().Be("The refresh token is not valid.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TokenCreatedSevenDaysMinusOneSecondAgo_When_Handling_Then_ReturnsSuccess()
    {
        var member = BuildMember();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var old = RefreshToken.Create(member.Id, OldToken, Now.AddDays(-7).AddSeconds(1));
        refreshTokens.Items[old.Id] = old;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RefreshHandler(refreshTokens, members, new FakeTokenService(), unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_TokenOfUnknownMember_When_Handling_Then_ReturnsInvalidAndDoesNotRevokeToken()
    {
        var refreshTokens = new FakeRefreshTokenRepository();
        var old = RefreshToken.Create(Guid.NewGuid(), OldToken, Now.AddHours(-1));
        refreshTokens.Items[old.Id] = old;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RefreshHandler(refreshTokens, new FakeMemberRepository(), new FakeTokenService(), unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        result.Error.Message.Should().Be("The refresh token is not valid.");
        old.RevokedAt.Should().BeNull();
        refreshTokens.Items.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TokenAlreadyUsedForRefresh_When_HandlingAgain_Then_ReturnsInvalid()
    {
        var member = BuildMember();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var old = RefreshToken.Create(member.Id, OldToken, Now.AddHours(-1));
        refreshTokens.Items[old.Id] = old;
        var handler = new RefreshHandler(refreshTokens, members, new FakeTokenService(), new FakeUnitOfWork(), new FixedTimeProvider(Now));
        await handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        var result = await handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        result.Error.Message.Should().Be("The refresh token is not valid.");
    }
}

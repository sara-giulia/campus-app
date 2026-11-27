using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Refresh;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Auth.Refresh;

public class RefreshHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string OldToken = "old-refresh-token";

    private readonly Member _member = Member.Create("Ana Pop", "ana@campus.ro", Now.AddDays(-30)).Value;
    private readonly FakeMemberRepository _members = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RefreshToken _stored;
    private readonly RefreshHandler _handler;

    public RefreshHandlerSuspendedTests()
    {
        _members.Items[_member.Id] = _member;
        _stored = RefreshToken.Create(_member.Id, OldToken, Now.AddHours(-1));
        _refreshTokens.Items[_stored.Id] = _stored;
        _handler = new RefreshHandler(_refreshTokens, _members, _tokens, _unitOfWork, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Given_SuspendedMemberWithActiveToken_When_Refreshing_Then_RevokesTokenAndReturnsAccountSuspended()
    {
        _member.Suspend(AdminId, "Scam attempts", null, Now.AddMinutes(-30));

        var result = await _handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Auth.AccountSuspended");
        result.Error.Message.Should().Be("This account is suspended permanently.");
        _stored.RevokedAt.Should().Be(Now);
        _refreshTokens.UpdateCount.Should().Be(1);
        _refreshTokens.Items.Should().ContainSingle();
        _tokens.LastMember.Should().BeNull();
        _tokens.RefreshTokenCount.Should().Be(0);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SuspendedMemberRefreshedOnce_When_RefreshingAgain_Then_TokenIsInvalid()
    {
        _member.Suspend(AdminId, "Scam attempts", null, Now.AddMinutes(-30));
        await _handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        var result = await _handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
        result.Error.Message.Should().Be("The refresh token is not valid.");
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_EndedSuspension_When_Refreshing_Then_RotatesTheToken()
    {
        _member.Suspend(AdminId, "Scam attempts", Now, Now.AddDays(-1));

        var result = await _handler.HandleAsync(new RefreshCommand(OldToken), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RefreshToken.Should().Be("refresh-1");
        _refreshTokens.Items.Should().HaveCount(2);
    }
}

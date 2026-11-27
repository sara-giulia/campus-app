using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Login;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Auth.Login;

public class LoginHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Member _member;
    private readonly FakeMemberRepository _members = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly LoginHandler _handler;

    public LoginHandlerSuspendedTests()
    {
        _member = Member.Register("Ana Pop", "ana@campus.ro", _hasher.Hash("secret12"), Now.AddDays(-30)).Value;
        _members.Items[_member.Id] = _member;
        _handler = new LoginHandler(_members, _hasher, _tokens, _refreshTokens, _unitOfWork, _clock);
    }

    private Task<Result<LoginResponse>> LoginAsync(string password) =>
        _handler.HandleAsync(new LoginCommand("ana@campus.ro", password), CancellationToken.None);

    [Fact]
    public async Task Given_TemporarilySuspendedMemberWithRightPassword_When_LoggingIn_Then_ReturnsAccountSuspendedAndIssuesNoToken()
    {
        _member.Suspend(AdminId, "Scam attempts", new DateTimeOffset(2026, 1, 8, 12, 30, 0, TimeSpan.Zero), Now.AddHours(-1));

        var result = await LoginAsync("secret12");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Auth.AccountSuspended");
        result.Error.Message.Should().Be("This account is suspended until 2026-01-08 12:30 UTC.");
        _tokens.LastMember.Should().BeNull();
        _tokens.RefreshTokenCount.Should().Be(0);
        _refreshTokens.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_PermanentlySuspendedMemberWithRightPassword_When_LoggingIn_Then_ReturnsAccountSuspendedPermanently()
    {
        _member.Suspend(AdminId, "Scam attempts", null, Now.AddHours(-1));

        var result = await LoginAsync("secret12");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Auth.AccountSuspended");
        result.Error.Message.Should().Be("This account is suspended permanently.");
        _refreshTokens.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_SuspendedMemberWithWrongPassword_When_LoggingIn_Then_ReturnsInvalidCredentials()
    {
        _member.Suspend(AdminId, "Scam attempts", null, Now.AddHours(-1));

        var result = await LoginAsync("wrong-password");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        result.Error.Message.Should().Be("The email or the password is wrong.");
        _tokens.RefreshTokenCount.Should().Be(0);
        _refreshTokens.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TemporarySuspensionOver_When_LoggingIn_Then_Succeeds()
    {
        _member.Suspend(AdminId, "Scam attempts", Now.AddDays(1), Now.AddHours(-1));
        _clock.Now = Now.AddDays(1);

        var result = await LoginAsync("secret12");

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be($"token-{_member.Id}");
        result.Value.RefreshToken.Should().Be("refresh-1");
        _refreshTokens.Items.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(1);
    }
}

using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Login;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Auth.Login;

public class LoginHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static Member BuildRegisteredMember(string email = "ada@ubb.ro", string password = "secret12") =>
        Member.Register("Ada", email, new FakePasswordHasher().Hash(password), Now).Value;

    [Fact]
    public async Task Given_ValidCredentials_When_Handling_Then_ReturnsTokenAndExpiresAt()
    {
        var member = BuildRegisteredMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var hasher = new FakePasswordHasher();
        var tokenService = new FakeTokenService();
        var handler = new LoginHandler(repository, hasher, tokenService);
        var command = new LoginCommand("ada@ubb.ro", "secret12");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be($"token-{member.Id}");
        result.Value.ExpiresAt.Should().Be(FakeTokenService.ExpiresAt);
        tokenService.LastMember.Should().BeSameAs(member);
    }

    [Fact]
    public async Task Given_EmailCasedDifferently_When_Handling_Then_ReturnsToken()
    {
        var member = BuildRegisteredMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var handler = new LoginHandler(repository, new FakePasswordHasher(), new FakeTokenService());
        var command = new LoginCommand("  ADA@UBB.RO  ", "secret12");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task Given_InvalidEmailFormat_When_Handling_Then_ReturnsInvalidCredentials(string email)
    {
        var repository = new FakeMemberRepository();
        var handler = new LoginHandler(repository, new FakePasswordHasher(), new FakeTokenService());
        var command = new LoginCommand(email, "secret12");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        result.Error.Message.Should().Be("The email or the password is wrong.");
    }

    [Fact]
    public async Task Given_EmptyPassword_When_Handling_Then_ReturnsInvalidCredentials()
    {
        var repository = new FakeMemberRepository();
        var handler = new LoginHandler(repository, new FakePasswordHasher(), new FakeTokenService());
        var command = new LoginCommand("ada@ubb.ro", "");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        result.Error.Message.Should().Be("The email or the password is wrong.");
    }

    [Fact]
    public async Task Given_UnknownEmail_When_Handling_Then_ReturnsInvalidCredentials()
    {
        var repository = new FakeMemberRepository();
        var handler = new LoginHandler(repository, new FakePasswordHasher(), new FakeTokenService());
        var command = new LoginCommand("ghost@ubb.ro", "secret12");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        result.Error.Message.Should().Be("The email or the password is wrong.");
    }

    [Fact]
    public async Task Given_MemberWithoutPasswordHash_When_Handling_Then_ReturnsInvalidCredentials()
    {
        var member = Member.Create("Ada", "ada@ubb.ro", Now).Value;
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var handler = new LoginHandler(repository, new FakePasswordHasher(), new FakeTokenService());
        var command = new LoginCommand("ada@ubb.ro", "secret12");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        result.Error.Message.Should().Be("The email or the password is wrong.");
    }

    [Fact]
    public async Task Given_WrongPassword_When_Handling_Then_ReturnsInvalidCredentials()
    {
        var member = BuildRegisteredMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var tokenService = new FakeTokenService();
        var handler = new LoginHandler(repository, new FakePasswordHasher(), tokenService);
        var command = new LoginCommand("ada@ubb.ro", "wrong-password");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        result.Error.Message.Should().Be("The email or the password is wrong.");
        tokenService.LastMember.Should().BeNull();
    }

    [Fact]
    public async Task Given_StoredMemberWithPassword_When_HandlingWithEmptyPassword_Then_ReturnsInvalidCredentialsWithoutCallingVerify()
    {
        var member = BuildRegisteredMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var hasher = new FakePasswordHasher();
        var handler = new LoginHandler(repository, hasher, new FakeTokenService());
        var command = new LoginCommand("ada@ubb.ro", "");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        result.Error.Message.Should().Be("The email or the password is wrong.");
        hasher.VerifyCount.Should().Be(0);
    }
}

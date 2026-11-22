using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.ResetPassword;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Auth.ResetPassword;

public class ResetPasswordHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private const string ValidPassword = "NewSecret12";
    private const string OldHash = "hashed:old";

    private static ResetPasswordHandler BuildHandler(
        FakePasswordResetTokenRepository tokens,
        FakeMemberRepository members,
        FakeRefreshTokenRepository refreshTokens,
        FakeUnitOfWork unitOfWork,
        FakePasswordHasher hasher,
        FakeEmailSender emailSender) =>
        new(tokens, members, refreshTokens, unitOfWork, hasher, emailSender, new FixedTimeProvider(Now));

    private static Member BuildMember()
    {
        var member = Member.Register("Ada", "ada@student.ro", OldHash, Now).Value;
        return member;
    }

    [Fact]
    public async Task Given_ValidTokenAndStrongPassword_When_Handling_Then_ChangesPasswordUsesTokenRevokesRefreshAndEmails()
    {
        var member = BuildMember();
        var (token, plain) = PasswordResetToken.Create(member.Id, Now);
        var activeRefresh = RefreshToken.Create(member.Id, "refresh-plain", Now);

        var tokens = new FakePasswordResetTokenRepository();
        tokens.Items[token.Id] = token;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        refreshTokens.Items[activeRefresh.Id] = activeRefresh;
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var emailSender = new FakeEmailSender();
        var handler = BuildHandler(tokens, members, refreshTokens, unitOfWork, hasher, emailSender);
        var command = new ResetPasswordCommand(plain, ValidPassword, ValidPassword);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(member.Id);
        member.PasswordHash.Should().Be(FakePasswordHasher.Prefix + ValidPassword);
        tokens.Items.Values.Single().UsedAt.Should().Be(Now);
        refreshTokens.Items.Values.Single().RevokedAt.Should().Be(Now);
        unitOfWork.SaveCount.Should().Be(1);

        emailSender.Items.Should().HaveCount(1);
        var email = emailSender.Items.Single();
        email.To.Should().Be("ada@student.ro");
        email.Subject.Should().Be("Your CampusMarket password was changed");
        email.Body.Should().NotContain(ValidPassword);
    }

    [Fact]
    public async Task Given_UnknownToken_When_Handling_Then_ReturnsInvalidAndChangesNothing()
    {
        var member = BuildMember();
        var tokens = new FakePasswordResetTokenRepository();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var emailSender = new FakeEmailSender();
        var handler = BuildHandler(tokens, members, refreshTokens, unitOfWork, hasher, emailSender);
        var command = new ResetPasswordCommand("unknown-token", ValidPassword, ValidPassword);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidResetToken");
        result.Error.Message.Should().Be("The reset token is not valid.");
        member.PasswordHash.Should().Be(OldHash);
        unitOfWork.SaveCount.Should().Be(0);
        emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ExpiredToken_When_Handling_Then_ReturnsInvalidAndChangesNothing()
    {
        var member = BuildMember();
        var issuedAt = Now.Subtract(PasswordResetToken.Lifetime).AddMinutes(-1);
        var (token, plain) = PasswordResetToken.Create(member.Id, issuedAt);

        var tokens = new FakePasswordResetTokenRepository();
        tokens.Items[token.Id] = token;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var emailSender = new FakeEmailSender();
        var handler = BuildHandler(tokens, members, refreshTokens, unitOfWork, hasher, emailSender);
        var command = new ResetPasswordCommand(plain, ValidPassword, ValidPassword);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidResetToken");
        result.Error.Message.Should().Be("The reset token is not valid.");
        result.Error.Type.Should().Be(ErrorType.Validation);
        member.PasswordHash.Should().Be(OldHash);
        tokens.Items.Values.Single().UsedAt.Should().BeNull();
        unitOfWork.SaveCount.Should().Be(0);
        emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_AlreadyUsedToken_When_Handling_Then_ReturnsInvalidAndChangesNothing()
    {
        var member = BuildMember();
        var (token, plain) = PasswordResetToken.Create(member.Id, Now);
        token.MarkUsed(Now);

        var tokens = new FakePasswordResetTokenRepository();
        tokens.Items[token.Id] = token;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var emailSender = new FakeEmailSender();
        var handler = BuildHandler(tokens, members, refreshTokens, unitOfWork, hasher, emailSender);
        var command = new ResetPasswordCommand(plain, ValidPassword, ValidPassword);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidResetToken");
        result.Error.Message.Should().Be("The reset token is not valid.");
        result.Error.Type.Should().Be(ErrorType.Validation);
        member.PasswordHash.Should().Be(OldHash);
        unitOfWork.SaveCount.Should().Be(0);
        emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_EmptyToken_When_Handling_Then_ReturnsInvalidAndChangesNothing()
    {
        var member = BuildMember();
        var tokens = new FakePasswordResetTokenRepository();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var emailSender = new FakeEmailSender();
        var handler = BuildHandler(tokens, members, refreshTokens, unitOfWork, hasher, emailSender);
        var command = new ResetPasswordCommand("   ", ValidPassword, ValidPassword);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidResetToken");
        result.Error.Message.Should().Be("The reset token is not valid.");
        result.Error.Type.Should().Be(ErrorType.Validation);
        member.PasswordHash.Should().Be(OldHash);
        unitOfWork.SaveCount.Should().Be(0);
        emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MissingMemberForToken_When_Handling_Then_ReturnsInvalidAndChangesNothing()
    {
        var member = BuildMember();
        var (token, plain) = PasswordResetToken.Create(member.Id, Now);
        var tokens = new FakePasswordResetTokenRepository();
        tokens.Items[token.Id] = token;
        var members = new FakeMemberRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var emailSender = new FakeEmailSender();
        var handler = BuildHandler(tokens, members, refreshTokens, unitOfWork, hasher, emailSender);
        var command = new ResetPasswordCommand(plain, ValidPassword, ValidPassword);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidResetToken");
        result.Error.Message.Should().Be("The reset token is not valid.");
        result.Error.Type.Should().Be(ErrorType.Validation);
        tokens.Items.Values.Single().UsedAt.Should().BeNull();
        unitOfWork.SaveCount.Should().Be(0);
        emailSender.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public async Task Given_WeakPassword_When_Handling_Then_ReturnsWeakPasswordAndChangesNothing(string weak)
    {
        var member = BuildMember();
        var (token, plain) = PasswordResetToken.Create(member.Id, Now);
        var tokens = new FakePasswordResetTokenRepository();
        tokens.Items[token.Id] = token;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var emailSender = new FakeEmailSender();
        var handler = BuildHandler(tokens, members, refreshTokens, unitOfWork, hasher, emailSender);
        var command = new ResetPasswordCommand(plain, weak, weak);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.WeakPassword");
        result.Error.Message.Should().Be("Password must have at least 8 characters.");
        member.PasswordHash.Should().Be(OldHash);
        tokens.Items.Values.Single().UsedAt.Should().BeNull();
        unitOfWork.SaveCount.Should().Be(0);
        emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MismatchedConfirmation_When_Handling_Then_ReturnsPasswordsDoNotMatchAndChangesNothing()
    {
        var member = BuildMember();
        var (token, plain) = PasswordResetToken.Create(member.Id, Now);
        var tokens = new FakePasswordResetTokenRepository();
        tokens.Items[token.Id] = token;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var emailSender = new FakeEmailSender();
        var handler = BuildHandler(tokens, members, refreshTokens, unitOfWork, hasher, emailSender);
        var command = new ResetPasswordCommand(plain, ValidPassword, "DifferentSecret12");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.PasswordsDoNotMatch");
        result.Error.Message.Should().Be("The two passwords are not the same.");
        member.PasswordHash.Should().Be(OldHash);
        tokens.Items.Values.Single().UsedAt.Should().BeNull();
        unitOfWork.SaveCount.Should().Be(0);
        emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_HasherReturnsEmptyHash_When_Handling_Then_ReturnsPasswordRequiredAndChangesNothing()
    {
        var member = BuildMember();
        var (token, plain) = PasswordResetToken.Create(member.Id, Now);
        var activeRefresh = RefreshToken.Create(member.Id, "refresh-plain", Now);
        var tokens = new FakePasswordResetTokenRepository();
        tokens.Items[token.Id] = token;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var refreshTokens = new FakeRefreshTokenRepository();
        refreshTokens.Items[activeRefresh.Id] = activeRefresh;
        var unitOfWork = new FakeUnitOfWork();
        var emailSender = new FakeEmailSender();
        var handler = new ResetPasswordHandler(
            tokens, members, refreshTokens, unitOfWork, new EmptyHashPasswordHasher(), emailSender, new FixedTimeProvider(Now));
        var command = new ResetPasswordCommand(plain, ValidPassword, ValidPassword);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Member.PasswordRequired");
        result.Error.Message.Should().Be("A password is required.");
        member.PasswordHash.Should().Be(OldHash);
        tokens.Items.Values.Single().UsedAt.Should().BeNull();
        refreshTokens.Items.Values.Single().RevokedAt.Should().BeNull();
        members.UpdateCount.Should().Be(0);
        tokens.UpdateCount.Should().Be(0);
        refreshTokens.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
        emailSender.Items.Should().BeEmpty();
    }

    private sealed class EmptyHashPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "";

        public bool Verify(string passwordHash, string password) => true;
    }
}

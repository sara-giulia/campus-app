using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.ForgotPassword;
using CampusMarket.UnitTests.Fakes;
using Microsoft.Extensions.Options;

namespace CampusMarket.UnitTests.Features.Auth.ForgotPassword;

public class ForgotPasswordHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static ForgotPasswordHandler BuildHandler(
        FakeMemberRepository members,
        FakePasswordResetTokenRepository tokens,
        FakeUnitOfWork unitOfWork,
        FakeEmailSender emailSender) =>
        new(
            members,
            tokens,
            unitOfWork,
            emailSender,
            Options.Create(new FrontendOptions { BaseUrl = "http://localhost:5173" }),
            new FixedTimeProvider(Now));

    [Fact]
    public async Task Given_ExistingEmail_When_Handling_Then_SavesTokenAndSendsEmailWithPlainTokenInLink()
    {
        var member = Member.Register("Ada", "ada@student.ro", "hashed:secret", Now).Value;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var tokens = new FakePasswordResetTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var emailSender = new FakeEmailSender();
        var handler = BuildHandler(members, tokens, unitOfWork, emailSender);
        var command = new ForgotPasswordCommand("ada@student.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ForgotPasswordResponse.Default);

        tokens.Items.Should().HaveCount(1);
        var saved = tokens.Items.Values.Single();
        saved.MemberId.Should().Be(member.Id);
        saved.ExpiresAt.Should().Be(Now.Add(PasswordResetToken.Lifetime));

        unitOfWork.SaveCount.Should().Be(1);

        emailSender.Items.Should().HaveCount(1);
        var email = emailSender.Items.Single();
        email.To.Should().Be("ada@student.ro");
        email.Subject.Should().Be("Reset your CampusMarket password");
        email.Body.Should().Contain("http://localhost:5173/reset-password?token=");

        var tokenInLink = ExtractTokenFromLink(email.Body);
        PasswordResetToken.Hash(tokenInLink).Should().Be(saved.TokenHash);
    }

    [Fact]
    public async Task Given_UnknownEmail_When_Handling_Then_ReturnsSameResponseAndSavesNothing()
    {
        var members = new FakeMemberRepository();
        var tokens = new FakePasswordResetTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var emailSender = new FakeEmailSender();
        var handler = BuildHandler(members, tokens, unitOfWork, emailSender);
        var command = new ForgotPasswordCommand("nobody@student.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ForgotPasswordResponse.Default);
        tokens.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
        emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_InvalidEmailFormat_When_Handling_Then_ReturnsSameResponseAndSavesNothing()
    {
        var members = new FakeMemberRepository();
        var tokens = new FakePasswordResetTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var emailSender = new FakeEmailSender();
        var handler = BuildHandler(members, tokens, unitOfWork, emailSender);
        var command = new ForgotPasswordCommand("not-an-email");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ForgotPasswordResponse.Default);
        tokens.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
        emailSender.Items.Should().BeEmpty();
    }

    private static string ExtractTokenFromLink(string body)
    {
        const string marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = body.IndexOfAny(new[] { '\r', '\n', ' ' }, start);
        return end < 0 ? body[start..] : body[start..end];
    }
}

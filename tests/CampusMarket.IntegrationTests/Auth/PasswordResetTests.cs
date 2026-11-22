using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.ForgotPassword;
using CampusMarket.Api.Features.Auth.Login;
using CampusMarket.Api.Features.Auth.ResetPassword;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusMarket.IntegrationTests.Auth;

public sealed class PasswordResetTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly char[] TokenLinkTerminators = ['\r', '\n', ' '];
    private const string OldPassword = "old-secret12";
    private const string NewPassword = "new-secret12";

    private static Member BuildMemberWithPassword(string email, string password)
    {
        var hasher = new AspNetPasswordHasher();
        return Member.Register("Ana Popescu", email, hasher.Hash(password), CreatedAt).Value;
    }

    // Replaces IEmailSender with a capturing fake so the test can read the
    // reset link; disposed with `await using` by the caller.
    private WebApplicationFactory<Program> WithCapturingEmailSender(CapturingEmailSender capturing) =>
        Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(capturing);
            }));

    [Fact]
    public async Task Given_RegisteredMember_When_FullResetFlow_Then_NewPasswordWorksAndOldFailsAndTokenUsed()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", OldPassword);
        await SeedAsync(member);
        var emails = new CapturingEmailSender();
        await using var seeded = WithCapturingEmailSender(emails);
        var client = seeded.CreateClient();

        // Act
        var forgotResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password", new ForgotPasswordRequest("ana@student.ro"), Json);

        // Assert (forgot)
        forgotResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var forgotBody = await forgotResponse.Content.ReadFromJsonAsync<ForgotPasswordResponse>(Json);
        forgotBody!.Message.Should().Be(ForgotPasswordResponse.Default.Message);
        emails.Items.Should().HaveCount(1);
        var resetEmail = emails.Items.Single();
        resetEmail.To.Should().Be("ana@student.ro");
        var plainToken = ExtractTokenFromLink(resetEmail.Body);
        plainToken.Should().NotBeNullOrEmpty();

        // Reset the password with the token from the email.
        var resetResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(plainToken, NewPassword, NewPassword),
            Json);
        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await resetResponse.Content.ReadAsStringAsync()).Should().BeEmpty();

        // The "password changed" email is captured and never contains the new password.
        emails.Items.Should().HaveCount(2);
        var changedEmail = emails.Items[1];
        changedEmail.To.Should().Be("ana@student.ro");
        changedEmail.Body.Should().NotContain(NewPassword);

        // Login with the new password succeeds.
        var newLogin = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest("ana@student.ro", NewPassword), Json);
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await newLogin.Content.ReadFromJsonAsync<LoginResponse>(Json);
        loginBody!.Token.Should().NotBeNullOrWhiteSpace();

        // Login with the old password fails.
        var oldLogin = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest("ana@student.ro", OldPassword), Json);
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The reset token is marked used in the database.
        var savedToken = await QueryDbAsync(db => db.Set<PasswordResetToken>().AsNoTracking().SingleAsync());
        savedToken.MemberId.Should().Be(member.Id);
        savedToken.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_UnknownEmail_When_ForgotPassword_Then_Returns202WithSameBodyAndSendsNothing()
    {
        // Arrange
        var emails = new CapturingEmailSender();
        await using var seeded = WithCapturingEmailSender(emails);
        var client = seeded.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password", new ForgotPasswordRequest("ghost@student.ro"), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>(Json);
        body!.Message.Should().Be(ForgotPasswordResponse.Default.Message);
        emails.Items.Should().BeEmpty();

        var tokenCount = await QueryDbAsync(db => db.Set<PasswordResetToken>().CountAsync());
        tokenCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TokenAlreadyUsed_When_ResettingAgain_Then_Returns400InvalidResetToken()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", OldPassword);
        await SeedAsync(member);
        var emails = new CapturingEmailSender();
        await using var seeded = WithCapturingEmailSender(emails);
        var client = seeded.CreateClient();

        var forgotResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password", new ForgotPasswordRequest("ana@student.ro"), Json);
        forgotResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var plainToken = ExtractTokenFromLink(emails.Items.Single().Body);

        var firstReset = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(plainToken, NewPassword, NewPassword),
            Json);
        firstReset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var secondReset = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(plainToken, "another-secret12", "another-secret12"),
            Json);

        // Assert
        secondReset.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await secondReset.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidResetToken");
        error.Type.Should().Be(ErrorType.Validation);

        var savedToken = await QueryDbAsync(db => db.Set<PasswordResetToken>().AsNoTracking().SingleAsync());
        savedToken.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_RandomToken_When_ResettingPassword_Then_Returns400InvalidResetToken()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", OldPassword);
        await SeedAsync(member);
        var emails = new CapturingEmailSender();
        await using var seeded = WithCapturingEmailSender(emails);
        var client = seeded.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest("not-a-real-reset-token", NewPassword, NewPassword),
            Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidResetToken");
        error.Type.Should().Be(ErrorType.Validation);

        emails.Items.Should().BeEmpty();
        var tokenCount = await QueryDbAsync(db => db.Set<PasswordResetToken>().CountAsync());
        tokenCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_WeakPassword_When_ResettingPassword_Then_Returns400WeakPassword()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", OldPassword);
        await SeedAsync(member);
        var emails = new CapturingEmailSender();
        await using var seeded = WithCapturingEmailSender(emails);
        var client = seeded.CreateClient();

        var forgotResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password", new ForgotPasswordRequest("ana@student.ro"), Json);
        forgotResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var plainToken = ExtractTokenFromLink(emails.Items.Single().Body);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(plainToken, "short", "short"),
            Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.WeakPassword");
        error.Type.Should().Be(ErrorType.Validation);

        var savedToken = await QueryDbAsync(db => db.Set<PasswordResetToken>().AsNoTracking().SingleAsync());
        savedToken.UsedAt.Should().BeNull();
        emails.Items.Should().HaveCount(1);
    }

    private static string ExtractTokenFromLink(string body)
    {
        const string marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = body.IndexOfAny(TokenLinkTerminators, start);
        return end < 0 ? body[start..] : body[start..end];
    }

    // Capturing IEmailSender: keeps every send in a thread-safe list so the
    // test can read the reset link sent to the member.
    private sealed class CapturingEmailSender : IEmailSender
    {
        private readonly ConcurrentQueue<SentEmail> _items = new();

        public List<SentEmail> Items => _items.ToList();

        public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
        {
            _items.Enqueue(new SentEmail(to, subject, body));
            return Task.CompletedTask;
        }
    }

    public sealed record SentEmail(string To, string Subject, string Body);
}

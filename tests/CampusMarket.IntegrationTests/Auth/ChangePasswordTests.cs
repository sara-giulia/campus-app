using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.ChangePassword;
using CampusMarket.Api.Features.Auth.Login;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Auth;

public sealed class ChangePasswordTests : IntegrationTestBase
{
    private const string Email = "ana@student.ro";
    private const string OldPassword = "secret12";
    private const string NewPassword = "new-secret34";
    private const string ChangePasswordUrl = "/api/v1/auth/change-password";
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly AspNetPasswordHasher Hasher = new();

    private static Member BuildMemberWithPassword(string email, string password) =>
        Member.Register("Ana Popescu", email, Hasher.Hash(password), CreatedAt).Value;

    private Task<Member> LoadMemberAsync(Guid id) =>
        QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync(m => m.Id == id));

    [Fact]
    public async Task Given_ValidRequest_When_ChangingPassword_Then_Returns204AndStoresNewHash()
    {
        // Arrange
        var member = BuildMemberWithPassword(Email, OldPassword);
        await SeedAsync(member);
        AuthenticateAs(member.Id);
        var request = new ChangePasswordRequest(OldPassword, NewPassword, NewPassword);

        // Act
        var response = await Client.PostAsJsonAsync(ChangePasswordUrl, request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await LoadMemberAsync(member.Id);
        Hasher.Verify(saved.PasswordHash!, NewPassword).Should().BeTrue();
        Hasher.Verify(saved.PasswordHash!, OldPassword).Should().BeFalse();
    }

    [Fact]
    public async Task Given_ChangedPassword_When_LoggingInWithNewPassword_Then_Returns200WithToken()
    {
        // Arrange
        var member = BuildMemberWithPassword(Email, OldPassword);
        await SeedAsync(member);
        AuthenticateAs(member.Id);
        var change = await Client.PostAsJsonAsync(
            ChangePasswordUrl, new ChangePasswordRequest(OldPassword, NewPassword, NewPassword), Json);
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var request = new LoginRequest(Email, NewPassword);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        body!.Token.Should().NotBeNullOrWhiteSpace();

        var saved = await LoadMemberAsync(member.Id);
        Hasher.Verify(saved.PasswordHash!, NewPassword).Should().BeTrue();
    }

    [Fact]
    public async Task Given_ChangedPassword_When_LoggingInWithOldPassword_Then_Returns401()
    {
        // Arrange
        var member = BuildMemberWithPassword(Email, OldPassword);
        await SeedAsync(member);
        AuthenticateAs(member.Id);
        var change = await Client.PostAsJsonAsync(
            ChangePasswordUrl, new ChangePasswordRequest(OldPassword, NewPassword, NewPassword), Json);
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var request = new LoginRequest(Email, OldPassword);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidCredentials");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await LoadMemberAsync(member.Id);
        Hasher.Verify(saved.PasswordHash!, OldPassword).Should().BeFalse();
    }

    [Fact]
    public async Task Given_WrongCurrentPassword_When_ChangingPassword_Then_Returns400AndKeepsHash()
    {
        // Arrange
        var member = BuildMemberWithPassword(Email, OldPassword);
        await SeedAsync(member);
        AuthenticateAs(member.Id);
        var request = new ChangePasswordRequest("wrong-password", NewPassword, NewPassword);

        // Act
        var response = await Client.PostAsJsonAsync(ChangePasswordUrl, request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidCredentials");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await LoadMemberAsync(member.Id);
        saved.PasswordHash.Should().Be(member.PasswordHash);
        Hasher.Verify(saved.PasswordHash!, OldPassword).Should().BeTrue();
    }

    [Fact]
    public async Task Given_NewPasswordOfSevenCharacters_When_ChangingPassword_Then_Returns400AndKeepsHash()
    {
        // Arrange
        var member = BuildMemberWithPassword(Email, OldPassword);
        await SeedAsync(member);
        AuthenticateAs(member.Id);
        var request = new ChangePasswordRequest(OldPassword, "short12", "short12");

        // Act
        var response = await Client.PostAsJsonAsync(ChangePasswordUrl, request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.WeakPassword");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await LoadMemberAsync(member.Id);
        saved.PasswordHash.Should().Be(member.PasswordHash);
        Hasher.Verify(saved.PasswordHash!, OldPassword).Should().BeTrue();
    }

    [Fact]
    public async Task Given_DifferentConfirmation_When_ChangingPassword_Then_Returns400AndKeepsHash()
    {
        // Arrange
        var member = BuildMemberWithPassword(Email, OldPassword);
        await SeedAsync(member);
        AuthenticateAs(member.Id);
        var request = new ChangePasswordRequest(OldPassword, NewPassword, "other-secret56");

        // Act
        var response = await Client.PostAsJsonAsync(ChangePasswordUrl, request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.PasswordsDoNotMatch");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await LoadMemberAsync(member.Id);
        saved.PasswordHash.Should().Be(member.PasswordHash);
        Hasher.Verify(saved.PasswordHash!, OldPassword).Should().BeTrue();
    }

    [Fact]
    public async Task Given_TokenOfUnknownMember_When_ChangingPassword_Then_Returns404()
    {
        // Arrange
        var member = BuildMemberWithPassword(Email, OldPassword);
        await SeedAsync(member);
        AuthenticateAs(Guid.NewGuid());
        var request = new ChangePasswordRequest(OldPassword, NewPassword, NewPassword);

        // Act
        var response = await Client.PostAsJsonAsync(ChangePasswordUrl, request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await LoadMemberAsync(member.Id);
        saved.PasswordHash.Should().Be(member.PasswordHash);
    }

    [Fact]
    public async Task Given_NoToken_When_ChangingPassword_Then_Returns401AndKeepsMember()
    {
        // Arrange
        var member = BuildMemberWithPassword(Email, OldPassword);
        await SeedAsync(member);
        var request = new ChangePasswordRequest(OldPassword, NewPassword, NewPassword);

        // Act
        var response = await Client.PostAsJsonAsync(ChangePasswordUrl, request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be(ChangePasswordUrl);
        problem.TraceId.Should().NotBeEmpty();

        var saved = await LoadMemberAsync(member.Id);
        saved.Name.Should().Be(member.Name);
        saved.Email.Value.Should().Be(member.Email.Value);
        saved.PasswordHash.Should().Be(member.PasswordHash);
    }
}

using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Login;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Auth;

public sealed class LoginTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Member BuildMemberWithPassword(string email, string password)
    {
        var hasher = new AspNetPasswordHasher();
        return Member.Register("Ana Popescu", email, hasher.Hash(password), CreatedAt).Value;
    }

    [Fact]
    public async Task Given_ValidCredentials_When_LoggingIn_Then_Returns200WithToken()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        await SeedAsync(member);
        var request = new LoginRequest("ana@student.ro", "secret12");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.ExpiresAt.Should().BeAfter(CreatedAt);

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(member.Id);
    }

    [Fact]
    public async Task Given_UnknownEmail_When_LoggingIn_Then_Returns401()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        await SeedAsync(member);
        var request = new LoginRequest("ghost@student.ro", "secret12");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidCredentials");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_WrongPassword_When_LoggingIn_Then_Returns401()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        await SeedAsync(member);
        var request = new LoginRequest("ana@student.ro", "wrong-password");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidCredentials");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_InvalidEmailFormat_When_LoggingIn_Then_Returns401()
    {
        // Arrange
        var request = new LoginRequest("not-an-email", "secret12");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidCredentials");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyPassword_When_LoggingIn_Then_Returns401()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        await SeedAsync(member);
        var request = new LoginRequest("ana@student.ro", "");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidCredentials");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_FivePreviousLoginAttempts_When_LoggingInAgain_Then_Returns429()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        await SeedAsync(member);
        var request = new LoginRequest("ana@student.ro", "wrong-password");
        await ExhaustLoginRateLimitAsync(request);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.TooManyRequests);
        problem.Code.Should().Be("Request.TooManyRequests");
        problem.Instance.Should().Be("/api/v1/auth/login");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(1);
    }

    private async Task ExhaustLoginRateLimitAsync(LoginRequest request)
    {
        await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);
        await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);
        await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);
        await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);
        await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);
    }
}

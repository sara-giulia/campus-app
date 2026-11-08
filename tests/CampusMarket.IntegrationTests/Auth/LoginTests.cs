using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Login;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        var before = DateTimeOffset.UtcNow;

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.ExpiresAt.Should().BeAfter(CreatedAt);
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.ExpiresAt.Should().BeOnOrAfter(before.AddMinutes(15))
            .And.BeOnOrBefore(DateTimeOffset.UtcNow.AddMinutes(15));

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(member.Id);

        var tokens = await QueryDbAsync(db => db.Set<RefreshToken>().AsNoTracking()
            .Where(t => t.MemberId == member.Id).ToListAsync());
        tokens.Should().ContainSingle();
        tokens[0].TokenHash.Should().Be(RefreshToken.Hash(body.RefreshToken));
        tokens[0].TokenHash.Should().NotBe(body.RefreshToken);
        tokens[0].RevokedAt.Should().BeNull();
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

    [Fact]
    public async Task Given_FiveAttemptsFromOneIp_When_LoggingInFromAnotherIp_Then_Returns200()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        await SeedAsync(member);
        await using var app = WithRemoteIp();
        var client = app.CreateClient();
        await ExhaustLoginRateLimitFromIpAsync(client, "10.0.0.1", new LoginRequest("ana@student.ro", "wrong-password"));
        var request = LoginFromIp("10.0.0.2", new LoginRequest("ana@student.ro", "secret12"));

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        body!.Token.Should().NotBeNullOrWhiteSpace();

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_FiveAttemptsFromOneIp_When_LoggingInAgainFromTheSameIp_Then_Returns429()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        await SeedAsync(member);
        await using var app = WithRemoteIp();
        var client = app.CreateClient();
        await ExhaustLoginRateLimitFromIpAsync(client, "10.0.0.1", new LoginRequest("ana@student.ro", "wrong-password"));
        var request = LoginFromIp("10.0.0.1", new LoginRequest("ana@student.ro", "secret12"));

        // Act
        var response = await client.SendAsync(request);

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

    // Starts a copy of the API that reads the remote IP from the X-Test-Remote-Ip header.
    private WebApplicationFactory<Program> WithRemoteIp() =>
        Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter, TestRemoteIpStartupFilter>()));

    private static HttpRequestMessage LoginFromIp(string ip, LoginRequest login)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(login, options: Json)
        };
        request.Headers.Add(TestRemoteIpStartupFilter.HeaderName, ip);
        return request;
    }

    private static async Task ExhaustLoginRateLimitFromIpAsync(HttpClient client, string ip, LoginRequest login)
    {
        for (var i = 0; i < 5; i++)
            await client.SendAsync(LoginFromIp(ip, login));
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

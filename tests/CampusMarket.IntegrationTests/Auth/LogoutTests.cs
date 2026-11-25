using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Logout;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Auth;

public sealed class LogoutTests : IntegrationTestBase
{
    private const string PlainToken = "plain-refresh-token";
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Member BuildMemberWithPassword(string email, string password)
    {
        var hasher = new AspNetPasswordHasher();
        return Member.Register("Ana Popescu", email, hasher.Hash(password), CreatedAt).Value;
    }

    [Fact]
    public async Task Given_ActiveToken_When_LoggingOut_Then_Returns204AndRevokesToken()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        var token = RefreshToken.Create(member.Id, PlainToken, DateTimeOffset.UtcNow);
        await SeedAsync(member, token);
        var request = new LogoutRequest(PlainToken);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/logout", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<RefreshToken>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(token.Id);
        saved.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_RevokedToken_When_LoggingOut_Then_Returns401()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        var token = RefreshToken.Create(member.Id, PlainToken, DateTimeOffset.UtcNow);
        token.Revoke(DateTimeOffset.UtcNow);
        await SeedAsync(member, token);
        var request = new LogoutRequest(PlainToken);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/logout", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidRefreshToken");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<RefreshToken>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(token.Id);
        saved.TokenHash.Should().Be(token.TokenHash);
        saved.RevokedAt.Should().Be(token.RevokedAt.ToMicroseconds());
        saved.ExpiresAt.Should().Be(token.ExpiresAt.ToMicroseconds());
    }

    [Fact]
    public async Task Given_UnknownToken_When_LoggingOut_Then_Returns401()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        await SeedAsync(member);
        var request = new LogoutRequest("unknown-refresh-token");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/logout", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidRefreshToken");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<RefreshToken>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyToken_When_LoggingOut_Then_Returns401()
    {
        // Arrange
        var request = new LogoutRequest("");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/logout", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidRefreshToken");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<RefreshToken>().CountAsync());
        count.Should().Be(0);
    }
}

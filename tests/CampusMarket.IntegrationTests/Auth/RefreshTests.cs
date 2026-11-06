using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Refresh;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Auth;

public sealed class RefreshTests : IntegrationTestBase
{
    private const string PlainToken = "plain-refresh-token";
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Member BuildMemberWithPassword(string email, string password)
    {
        var hasher = new AspNetPasswordHasher();
        return Member.Register("Ana Popescu", email, hasher.Hash(password), CreatedAt).Value;
    }

    [Fact]
    public async Task Given_ActiveToken_When_Refreshing_Then_Returns200AndRotatesToken()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        var token = RefreshToken.Create(member.Id, PlainToken, DateTimeOffset.UtcNow);
        await SeedAsync(member, token);
        var request = new RefreshRequest(PlainToken);
        var before = DateTimeOffset.UtcNow;

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RefreshResponse>(Json);
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBe(PlainToken);
        body.ExpiresAt.Should().BeOnOrAfter(before.AddMinutes(15))
            .And.BeOnOrBefore(DateTimeOffset.UtcNow.AddMinutes(15));

        var tokens = await QueryDbAsync(db => db.Set<RefreshToken>().AsNoTracking()
            .Where(t => t.MemberId == member.Id).ToListAsync());
        tokens.Should().HaveCount(2);

        var old = tokens.Single(t => t.Id == token.Id);
        old.RevokedAt.Should().NotBeNull();

        var created = tokens.Single(t => t.Id != token.Id);
        created.TokenHash.Should().Be(RefreshToken.Hash(body.RefreshToken));
        created.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_RevokedToken_When_Refreshing_Then_Returns401()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        var token = RefreshToken.Create(member.Id, PlainToken, DateTimeOffset.UtcNow);
        token.Revoke(DateTimeOffset.UtcNow);
        await SeedAsync(member, token);
        var request = new RefreshRequest(PlainToken);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidRefreshToken");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<RefreshToken>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(token.Id);
        saved.TokenHash.Should().Be(token.TokenHash);
        saved.RevokedAt.Should().Be(token.RevokedAt);
        saved.ExpiresAt.Should().Be(token.ExpiresAt);
    }

    [Fact]
    public async Task Given_ExpiredToken_When_Refreshing_Then_Returns401()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        var token = RefreshToken.Create(member.Id, PlainToken, DateTimeOffset.UtcNow.AddDays(-8));
        await SeedAsync(member, token);
        var request = new RefreshRequest(PlainToken);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidRefreshToken");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<RefreshToken>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(token.Id);
        saved.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_UnknownToken_When_Refreshing_Then_Returns401()
    {
        // Arrange
        var member = BuildMemberWithPassword("ana@student.ro", "secret12");
        await SeedAsync(member);
        var request = new RefreshRequest("unknown-refresh-token");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidRefreshToken");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<RefreshToken>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyToken_When_Refreshing_Then_Returns401()
    {
        // Arrange
        var request = new RefreshRequest("");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidRefreshToken");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<RefreshToken>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_TokenOfMissingMember_When_Refreshing_Then_Returns401AndDoesNotRevoke()
    {
        // Arrange
        var missingMemberId = Guid.NewGuid();
        var token = RefreshToken.Create(missingMemberId, PlainToken, DateTimeOffset.UtcNow);
        await SeedAsync(token);
        var request = new RefreshRequest(PlainToken);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidRefreshToken");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<RefreshToken>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(token.Id);
        saved.RevokedAt.Should().BeNull();
    }
}

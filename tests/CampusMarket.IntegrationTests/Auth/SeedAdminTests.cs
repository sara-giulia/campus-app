using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Login;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Auth;

public sealed class SeedAdminTests : IntegrationTestBase
{
    private const string AdminPassword = "admin-secret12";
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // Starts a second copy of the API on the same in-memory database, with the admin seed turned on.
    private WebApplicationFactory<Program> WithAdminSeed(string adminEmail) =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:AdminEmail", adminEmail);
            builder.UseSetting("Seed:AdminPassword", AdminPassword);
        });

    [Fact]
    public async Task Given_AdminEmailWithUpperCaseAndSpacesInConfig_When_AppStarts_Then_SeedsAdminWithNormalizedEmail()
    {
        // Arrange
        await using var app = WithAdminSeed("  Admin@Campus.Test  ");
        var request = new LoginRequest("admin@campus.test", AdminPassword);

        // Act
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        body!.Token.Should().NotBeNullOrWhiteSpace();

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Email.Value.Should().Be("admin@campus.test");
        saved.Role.Should().Be(MemberRole.Admin);
    }

    [Fact]
    public async Task Given_AdminAlreadySeeded_When_AppStartsWithUpperCaseEmail_Then_DoesNotCreateSecondAdmin()
    {
        // Arrange
        var hasher = new AspNetPasswordHasher();
        var existing = Member.Register("Admin", "admin@campus.test", hasher.Hash(AdminPassword), CreatedAt, MemberRole.Admin).Value;
        await SeedAsync(existing);
        await using var app = WithAdminSeed("Admin@Campus.Test");
        var request = new LoginRequest("admin@campus.test", AdminPassword);

        // Act
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        body!.Token.Should().NotBeNullOrWhiteSpace();

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(existing.Id);
    }

    [Fact]
    public async Task Given_InvalidAdminEmailInConfig_When_AppStarts_Then_DoesNotSeedAdmin()
    {
        // Arrange
        await using var app = WithAdminSeed("not-an-email");

        // Act
        using var client = app.CreateClient();
        var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("CampusMarket API is running");

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }
}
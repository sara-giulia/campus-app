using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Login;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Auth;

public sealed class SeedAdminTests : IntegrationTestBase
{
    private const string AdminPassword = "admin-secret12";
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // Starts a copy of the API on the same in-memory database with the Seed:Admin* settings.
    // The seed runs at the first CreateClient() call on the returned factory.
    private WebApplicationFactory<Program> WithAdminSeed(string adminEmail, string adminPassword = AdminPassword) =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:AdminEmail", adminEmail);
            builder.UseSetting("Seed:AdminPassword", adminPassword);
        });

    [Fact]
    public async Task Given_AdminEmailWithWhitespaceAndMixedCase_When_StartingTheApp_Then_SeedsOneAdminAndLoginSucceeds()
    {
        // Arrange
        await using var seeded = WithAdminSeed("  Admin@Campus.Test  ");
        var client = seeded.CreateClient();
        var request = new LoginRequest("admin@campus.test", AdminPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        body!.Token.Should().NotBeNullOrWhiteSpace();

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        members.Should().HaveCount(1);
        members[0].Email.Value.Should().Be("admin@campus.test");
        members[0].Role.Should().Be(MemberRole.Admin);
    }

    [Fact]
    public async Task Given_TheAppStartsTwiceWithTheSameAdminEmail_When_LoggingIn_Then_KeepsOneAdmin()
    {
        // Arrange
        await using (var firstStart = WithAdminSeed("Admin@Campus.Test"))
        {
            firstStart.CreateClient();
        }

        await using var secondStart = WithAdminSeed("Admin@Campus.Test");
        var client = secondStart.CreateClient();
        var request = new LoginRequest("admin@campus.test", AdminPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_AnAdminAlreadyInTheDatabase_When_StartingTheAppWithTheSameEmail_Then_KeepsTheExistingAdmin()
    {
        // Arrange
        var hasher = new AspNetPasswordHasher();
        var existing = Member.Register("Admin", "admin@campus.test", hasher.Hash(AdminPassword), CreatedAt, MemberRole.Admin).Value;
        await SeedAsync(existing);

        await using var seeded = WithAdminSeed("Admin@Campus.Test");
        var client = seeded.CreateClient();
        var request = new LoginRequest("admin@campus.test", AdminPassword);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        members.Should().HaveCount(1);
        members[0].Id.Should().Be(existing.Id);
    }

    [Fact]
    public async Task Given_InvalidAdminEmail_When_StartingTheApp_Then_SeedsNothing()
    {
        // Arrange
        await using var seeded = WithAdminSeed("not-an-email");
        var client = seeded.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("CampusMarket API is running");

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyAdminPassword_When_StartingTheApp_Then_SeedsNothing()
    {
        // Arrange
        await using var seeded = WithAdminSeed("admin@campus.test", adminPassword: "");
        var client = seeded.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("CampusMarket API is running");

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }
}

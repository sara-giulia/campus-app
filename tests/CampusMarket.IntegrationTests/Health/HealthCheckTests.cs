using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Health;

public sealed class HealthCheckTests : IntegrationTestBase
{
    private static readonly Guid StudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Given_NoToken_When_GettingHealth_Then_Returns200Healthy()
    {
        // Act
        var response = await Client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");

        var listings = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        var members = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        listings.Should().Be(0);
        members.Should().Be(0);
    }

    [Fact]
    public async Task Given_StudentToken_When_GettingHealth_Then_Returns200Healthy()
    {
        // Arrange
        AuthenticateAs(StudentId);

        // Act
        var response = await Client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");

        var listings = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        var members = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        listings.Should().Be(0);
        members.Should().Be(0);
    }

    [Fact]
    public async Task Given_VersionedPath_When_GettingHealth_Then_Returns404ProblemDetails()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        problem.Code.Should().Be("Route.NotFound");
        problem.Instance.Should().Be("/api/v1/health");
        problem.TraceId.Should().NotBeEmpty();

        var listings = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        var members = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        listings.Should().Be(0);
        members.Should().Be(0);
    }
}

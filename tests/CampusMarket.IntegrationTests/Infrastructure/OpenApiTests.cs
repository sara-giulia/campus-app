using System.Net;
using CampusMarket.Api.Domain.Listings;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Infrastructure;

// The test host runs in Development, so the OpenAPI document is mapped at /openapi/v1.json.
public sealed class OpenApiTests : IntegrationTestBase
{
    [Fact]
    public async Task Given_DevelopmentEnvironment_When_GettingOpenApiDocument_Then_Returns200WithApiPaths()
    {
        // Act
        var response = await Client.GetAsync("/openapi/v1.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("/api/v1/listings");
        body.Should().Contain("/api/v1/auth/login");

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }
}

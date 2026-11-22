using System.Net;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Infrastructure.ML;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CampusMarket.IntegrationTests.Infrastructure;

public sealed class HealthTests : IntegrationTestBase
{
    [Fact]
    public async Task Given_NoToken_When_GettingHealthLive_Then_Returns200Healthy()
    {
        // Act
        var response = await Client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
        await AssertDatabaseIsEmptyAsync();
    }

    [Fact]
    public async Task Given_NoToken_When_GettingHealthReady_Then_Returns200Healthy()
    {
        // Act
        var response = await Client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
        await AssertDatabaseIsEmptyAsync();
    }

    [Fact]
    public async Task Given_NoToken_When_GettingHealth_Then_Returns200Healthy()
    {
        // Act
        var response = await Client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
        await AssertDatabaseIsEmptyAsync();
    }

    [Fact]
    public async Task Given_ModelsFolderDoesNotExist_When_CheckingMlModels_Then_ReturnsUnhealthyListingAllFiles()
    {
        // Arrange
        var options = Options.Create(new MlOptions { ModelsPath = "no-such-models-folder" });
        var check = new MlModelsHealthCheck(options);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("price-regression.zip")
            .And.Contain("category-classification.zip")
            .And.Contain("spam-detection.zip")
            .And.Contain("similar-listings.zip");
        await AssertDatabaseIsEmptyAsync();
    }

    private async Task AssertDatabaseIsEmptyAsync()
    {
        var listings = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        var members = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        listings.Should().Be(0);
        members.Should().Be(0);
    }
}

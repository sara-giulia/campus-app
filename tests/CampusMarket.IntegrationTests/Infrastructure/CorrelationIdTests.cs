using System.Net;
using CampusMarket.Api.Infrastructure.Logging;
using FluentAssertions;

namespace CampusMarket.IntegrationTests.Infrastructure;

public sealed class CorrelationIdTests : IntegrationTestBase
{
    [Fact]
    public async Task Given_NoCorrelationIdHeader_When_GettingListings_Then_ResponseHasNewGuidId()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/listings");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values).Should().BeTrue();
        var id = values!.Single();
        id.Should().NotBeEmpty();
        Guid.TryParse(id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Given_CorrelationIdHeader_When_GettingListings_Then_ResponseHasTheSameId()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/listings");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "test-123");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Should().Equal("test-123");
    }

    [Fact]
    public async Task Given_UnknownListing_When_GettingListingById_Then_Returns404WithCorrelationId()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/listings/{Guid.NewGuid()}");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values).Should().BeTrue();
        values!.Single().Should().NotBeEmpty();
    }
}

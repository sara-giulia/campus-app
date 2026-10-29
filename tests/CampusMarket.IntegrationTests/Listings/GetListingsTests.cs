using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class GetListingsTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_TwoListings_When_GettingListings_Then_Returns200WithBothListings()
    {
        // Arrange
        var manual = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        var calculator = Listing.Create(SellerId, "Calculator stiintific", Category.Electronics, Condition.New, 120m, CreatedAt).Value;
        manual.Publish();
        calculator.Publish();
        await SeedAsync(manual, calculator);

        // Act
        var response = await Client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Count.Should().Be(2);
        body.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { manual.Id, calculator.Id });

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task Given_EmptyDatabase_When_GettingListings_Then_Returns200WithNoListings()
    {
        // Arrange: the database starts empty

        // Act
        var response = await Client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Count.Should().Be(0);
        body.Items.Should().BeEmpty();

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }
}

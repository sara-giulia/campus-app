using System.Net;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Moderation;

// Uses the real spam model (ml-models/spam-detection.zip).
public sealed class SpamOnPublishTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_SpamDraft_When_SellerPublishes_Then_Returns204AndAddsAutomaticReport()
    {
        // Arrange
        var listing = Listing.Create(
            SellerId, "BANI RAPIZI, SUNA ACUM!!!", Category.Other, Condition.Good, 1m, CreatedAt,
            description: "Metoda sigura de castig online!!! Suna la 0739 465 387.").Value;
        await SeedAsync(listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/publish", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);

        var report = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync());
        report.ListingId.Should().Be(listing.Id);
        report.ReporterId.Should().BeNull();
        report.SpamProbability.Should().BeInRange(0.5, 1.0);
    }

    [Fact]
    public async Task Given_NormalDraft_When_SellerPublishes_Then_Returns204AndAddsNoReport()
    {
        // Arrange
        var listing = Listing.Create(
            SellerId, "Manual Algebra liniara, editia 2", Category.Textbook, Condition.Good, 40m, CreatedAt,
            description: "Putin folosit, fara sublinieri.").Value;
        await SeedAsync(listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/publish", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);

        var count = await QueryDbAsync(db => db.Set<Report>().CountAsync());
        count.Should().Be(0);
    }
}

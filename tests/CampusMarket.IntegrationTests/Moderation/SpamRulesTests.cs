using System.Net;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Moderation;

// Uses the real spam model (ml-models/spam-detection.zip) together with SpamRules.
public sealed class SpamRulesTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Manual de fizica", "Plata in avans prin transfer")]
    [InlineData("Manual de fizica", "Detalii pe www.manuale-ieftine.com")]
    [InlineData("Calculator Casio fx-991", "Sunati la 0722 123 456")]
    [InlineData("Halat laborator", "Mesaj la @halat_m")]
    [InlineData("Manual POO ieftin!!!", "Stare foarte buna.")]
    [InlineData("Câștigă bani ușor", "Fără investiție")]
    public async Task Given_TextCaughtOnlyByARule_When_SellerPublishes_Then_Returns204AndAddsReportWithRuleProbability(
        string title, string description)
    {
        // Arrange
        var listingId = await SeedDraftAsync(title, description);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listingId}/publish", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync(l => l.Id == listingId));
        saved.Status.Should().Be(ListingStatus.Active);

        var report = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync(r => r.ListingId == listingId));
        report.IsAutomatic.Should().BeTrue();
        report.ReporterId.Should().BeNull();
        report.SpamProbability.Should().Be(0.9);
    }

    [Fact]
    public async Task Given_TextFlaggedByTheModel_When_SellerPublishes_Then_Returns204AndAddsReportWithModelProbability()
    {
        // Arrange
        var listingId = await SeedDraftAsync(
            "Vand cont Netflix premium ieftin", "Scrie-mi pe whatsapp pentru detalii");

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listingId}/publish", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync(l => l.Id == listingId));
        saved.Status.Should().Be(ListingStatus.Active);

        var report = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync(r => r.ListingId == listingId));
        report.IsAutomatic.Should().BeTrue();
        report.ReporterId.Should().BeNull();
        report.SpamProbability.Should().BeGreaterThan(0.9);
    }

    [Theory]
    [InlineData("Manual chimie organica", "Folosit un semestru, cateva notite pe margine")]
    [InlineData("Halat laborator", "Marimea M, contact: ana.pop@student.ro")]
    public async Task Given_NormalText_When_SellerPublishes_Then_Returns204AndAddsNoReport(
        string title, string description)
    {
        // Arrange
        var listingId = await SeedDraftAsync(title, description);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listingId}/publish", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync(l => l.Id == listingId));
        saved.Status.Should().Be(ListingStatus.Active);

        var count = await QueryDbAsync(db => db.Set<Report>().CountAsync(r => r.ListingId == listingId));
        count.Should().Be(0);
    }

    // Seeds the seller and a draft of theirs, and logs in as the seller.
    private async Task<Guid> SeedDraftAsync(string title, string description)
    {
        var seller = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, title, Category.Other, Condition.Good, 40m, CreatedAt, description).Value;
        await SeedAsync(seller, listing);
        AuthenticateAs(seller.Id);
        return listing.Id;
    }
}

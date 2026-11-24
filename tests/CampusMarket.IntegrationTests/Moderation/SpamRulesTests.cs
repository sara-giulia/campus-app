using System.Net;
using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Infrastructure.ML;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using Microsoft.Extensions.Options;

namespace CampusMarket.IntegrationTests.Moderation;

// Uses the real spam model (ml-models/spam-detection.zip) together with SpamRules.
public sealed class SpamRulesTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private const decimal Price = 40m;

    [Theory]
    [InlineData("Manual de fizica", "Plata in avans")]
    [InlineData("Manual de fizica", "Vezi www.fizica-liceu.ro")]
    [InlineData("Calculator Casio fx-991", "Sunati la 0722 123 456")]
    [InlineData("Halat laborator", "Mesaj la @halat_m")]
    [InlineData("Manual POO ieftin!!!", "Stare foarte buna.")]
    [InlineData("Câștigă bani ușor", "Fără investiție")]
    public async Task Given_TextCaughtOnlyByARule_When_SellerPublishes_Then_Returns204AndAddsReportWithRuleProbability(
        string title, string description)
    {
        // Arrange
        // Precondition: the real model alone must not flag this text, otherwise the test no longer shows a rule.
        var threshold = Factory.Services.GetRequiredService<IOptions<SpamDetectionOptions>>().Value.Threshold;
        ModelProbability(title, description).Should().BeLessThan(threshold,
            "the text must be caught only by a rule; if a retrained model flags it, pick another text for the same rule");
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
        report.SpamProbability.Should().Be(SpamRules.RuleProbability);
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

    // The real model's probability for the text, as SpamDetector computes it before the rules.
    private double ModelProbability(string title, string description)
    {
        var pool = Factory.Services.GetRequiredService<PredictionEnginePool<SpamInput, SpamPrediction>>();
        var input = new SpamInput { Title = title, Description = description, Price = (float)Price };
        return pool.Predict(SpamDetector.ModelName, input).Probability;
    }

    // Seeds the seller and a draft of theirs, and logs in as the seller.
    private async Task<Guid> SeedDraftAsync(string title, string description)
    {
        var seller = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, title, Category.Other, Condition.Good, Price, CreatedAt, description).Value;
        await SeedAsync(seller, listing);
        AuthenticateAs(seller.Id);
        return listing.Id;
    }
}

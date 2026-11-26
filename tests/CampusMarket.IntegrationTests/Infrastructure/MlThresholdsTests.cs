using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampusMarket.Api.Common.Categories;
using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Conversations;
using CampusMarket.Api.Features.Listings.SuggestCategory;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CampusMarket.IntegrationTests.Infrastructure;

// The ML decision thresholds are settings (Ml:Category:MinConfidence, Ml:Spam:Threshold), validated at startup.
// A different value goes through a copy of the API on the same database, like SeedAdminTests.
public sealed class MlThresholdsTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // The API's appsettings.json as it is copied to the test output, read on its own (not through the test host),
    // so the test follows the value chosen on validation instead of a number written here.
    private static IConfigurationRoot AppSettings() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

    private WebApplicationFactory<Program> WithSetting(string key, string value) =>
        Factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

    [Fact]
    public async Task Given_DefaultHost_When_ResolvingThresholdOptions_Then_ReadsAppSettingsValues()
    {
        // Arrange
        var services = Factory.Services;
        var expected = AppSettings();

        // Act
        var category = services.GetRequiredService<IOptions<CategorySuggestionOptions>>().Value;
        var spam = services.GetRequiredService<IOptions<SpamDetectionOptions>>().Value;

        // Assert
        category.MinConfidence.Should().Be(expected.GetValue<double>("Ml:Category:MinConfidence"));
        spam.Threshold.Should().Be(expected.GetValue<double>("Ml:Spam:Threshold"));

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_MinConfidence099_When_SuggestingCategoryForNotes_Then_Returns200WithNullCategory()
    {
        // Arrange
        await using var strict = WithSetting("Ml:Category:MinConfidence", "0.99");
        var client = strict.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(SellerId));
        var request = new SuggestCategoryRequest(Title: "Notite Analiza matematica, an I");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/listings/category-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SuggestCategoryResponse>(Json);
        body!.Category.Should().BeNull();
        body.Confidence.Should().BeLessThan(0.99);
        body.Confidence.Should().BeGreaterThanOrEqualTo(0.6);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_SpamThreshold0_When_SellerPublishesNormalDraft_Then_Returns204AndAddsAutomaticReport()
    {
        // Arrange
        var listing = Listing.Create(
            SellerId, "Manual Algebra liniara, editia 2", Category.Textbook, Condition.Good, 40m, CreatedAt,
            description: "Putin folosit, fara sublinieri.").Value;
        await SeedAsync(listing);

        await using var everythingIsSpam = WithSetting("Ml:Spam:Threshold", "0");
        var client = everythingIsSpam.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(SellerId));

        // Act
        var response = await client.PostAsync($"/api/v1/listings/{listing.Id}/publish", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);

        var report = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync());
        report.ListingId.Should().Be(listing.Id);
        report.ReporterId.Should().BeNull();
    }

    [Fact]
    public async Task Given_MinConfidenceAbove1_When_StartingTheApp_Then_ThrowsOptionsValidationException()
    {
        // Arrange
        await using var invalid = WithSetting("Ml:Category:MinConfidence", "1.5");

        // Act
        Action act = () => invalid.CreateClient();

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Ml:Category:MinConfidence*");

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NegativeSpamThreshold_When_StartingTheApp_Then_ThrowsOptionsValidationException()
    {
        // Arrange
        await using var invalid = WithSetting("Ml:Spam:Threshold", "-0.1");

        // Act
        Action act = () => invalid.CreateClient();

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Ml:Spam:Threshold*");

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_DefaultHost_When_ResolvingMessageSpamOptions_Then_ReadsAppSettingsValues()
    {
        // Arrange
        var services = Factory.Services;
        var expected = AppSettings();

        // Act
        var messageSpam = services.GetRequiredService<IOptions<MessageSpamOptions>>().Value;

        // Assert
        messageSpam.Threshold.Should().Be(expected.GetValue<double>("Ml:MessageSpam:Threshold"));
        messageSpam.BlockThreshold.Should().Be(expected.GetValue<double>("Ml:MessageSpam:BlockThreshold"));

        var count = await QueryDbAsync(db => db.Set<Message>().CountAsync());
        count.Should().Be(0);
    }

    [Theory]
    [InlineData("Ml:MessageSpam:BlockThreshold", "1.5")]
    [InlineData("Ml:MessageSpam:Threshold", "-0.1")]
    [InlineData("Ml:MessageSpam:BlockThreshold", "0.5")]
    public async Task Given_InvalidMessageSpamSetting_When_StartingTheApp_Then_ThrowsOptionsValidationException(string key, string value)
    {
        // Arrange
        await using var invalid = WithSetting(key, value);

        // Act
        Action act = () => invalid.CreateClient();

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Ml:MessageSpam:Threshold and BlockThreshold*");

        var count = await QueryDbAsync(db => db.Set<Message>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_MessageSpamBlockThreshold1_When_SendingAScam_Then_Returns201Suspicious()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30));
        await SeedAsync(seller, buyer, listing);

        await using var neverBlocks = WithSetting("Ml:MessageSpam:BlockThreshold", "1");
        var client = neverBlocks.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(buyer.Id));
        var request = new SendMessageRequest(
            "Am platit prin Cargus, intra pe cargus-plati.com si introdu cardul ca sa primesti banii");

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.IsSuspicious.Should().BeTrue();

        var saved = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(body.Id);
        saved.IsSuspicious.Should().BeTrue();
        saved.SpamProbability.Should().NotBeNull();
    }
}

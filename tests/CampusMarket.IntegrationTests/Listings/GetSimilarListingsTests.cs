using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.GetSimilarListings;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Listings;

// Uses the real similar-listings model (ml-models/similar-listings.zip). All calls are public: no token.
public sealed class GetSimilarListingsTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_MathTextbook_When_GettingSimilarListings_Then_Returns200WithOtherMathTextbookFirst()
    {
        // Arrange
        var listings = await SeedRankingListingsAsync();

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{listings.AnalysisTextbook.Id}/similar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetSimilarListingsResponse>(Json);
        body!.Items.Should().HaveCountLessThanOrEqualTo(GetSimilarListingsQuery.MaxResults);
        body.Items[0].Id.Should().Be(listings.AnalysisWorkbook.Id);
        body.Items[0].Title.Should().Be("Culegere Analiza matematica, exercitii rezolvate");
        body.Items.Select(i => i.Id).Should()
            .NotContain(listings.AnalysisTextbook.Id)
            .And.NotContain(listings.DraftTextbook.Id);
        body.Items.Select(i => i.Similarity).Should().OnlyContain(s => s >= 0 && s <= 1)
            .And.BeInDescendingOrder();

        var itemIds = body.Items.Select(i => i.Id).ToList();
        var statuses = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Where(l => itemIds.Contains(l.Id))
            .Select(l => l.Status)
            .ToListAsync());
        statuses.Should().HaveCount(itemIds.Count).And.OnlyContain(s => s == ListingStatus.Active);
        await AssertListingCountAsync(7);
    }

    [Fact]
    public async Task Given_Laptop_When_GettingSimilarListings_Then_Returns200WithOtherLaptopFirst()
    {
        // Arrange
        var listings = await SeedRankingListingsAsync();

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{listings.LenovoLaptop.Id}/similar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetSimilarListingsResponse>(Json);
        body!.Items[0].Id.Should().Be(listings.DellLaptop.Id);
        body.Items.Select(i => i.Id).Should().NotContain(listings.LenovoLaptop.Id);

        await AssertListingCountAsync(7);
    }

    [Fact]
    public async Task Given_DraftTextbook_When_GettingSimilarListings_Then_Returns200WithMathTextbookFirst()
    {
        // Arrange
        var listings = await SeedRankingListingsAsync();

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{listings.DraftTextbook.Id}/similar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetSimilarListingsResponse>(Json);
        new[] { listings.AnalysisTextbook.Id, listings.AnalysisWorkbook.Id }.Should().Contain(body!.Items[0].Id);
        body.Items.Select(i => i.Id).Should().NotContain(listings.DraftTextbook.Id);

        var draft = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .SingleAsync(l => l.Id == listings.DraftTextbook.Id));
        draft.Status.Should().Be(ListingStatus.Draft);
        await AssertListingCountAsync(7);
    }

    [Fact]
    public async Task Given_TargetIsOnlyPublishedListing_When_GettingSimilarListings_Then_Returns200WithEmptyItems()
    {
        // Arrange
        var target = Published("Manual Analiza matematica, editia 2", Category.Textbook, 40m, CreatedAt);
        var draft = Listing.Create(
            SellerId, "Manual Analiza matematica, editia 1", Category.Textbook, Condition.Worn, 25m,
            CreatedAt.AddHours(1)).Value;
        await SeedAsync(target, draft);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{target.Id}/similar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetSimilarListingsResponse>(Json);
        body!.Items.Should().BeEmpty();

        await AssertListingCountAsync(2);
    }

    [Fact]
    public async Task Given_UnknownId_When_GettingSimilarListings_Then_Returns404()
    {
        // Arrange
        var listing = Published("Manual Analiza matematica, editia 2", Category.Textbook, 40m, CreatedAt);
        await SeedAsync(listing);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{Guid.NewGuid()}/similar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(listing);
    }

    private async Task<RankingListings> SeedRankingListingsAsync()
    {
        var listings = new RankingListings(
            AnalysisTextbook: Published(
                "Manual Analiza matematica, editia 2", Category.Textbook, 40m, CreatedAt,
                "Manual pentru anul I, cu exercitii rezolvate."),
            AnalysisWorkbook: Published(
                "Culegere Analiza matematica, exercitii rezolvate", Category.Textbook, 30m, CreatedAt.AddHours(1),
                "Culegere pentru examen, exercitii rezolvate pe capitole."),
            LenovoLaptop: Published(
                "Laptop Lenovo ThinkPad, i5, 8GB", Category.Electronics, 1500m, CreatedAt.AddHours(2),
                "Laptop pentru facultate, baterie buna, 8GB RAM."),
            DellLaptop: Published(
                "Laptop Dell Latitude, i7, 16GB", Category.Electronics, 2200m, CreatedAt.AddHours(3),
                "Laptop rapid, 16GB RAM, SSD 512GB."),
            Notes: Published(
                "Notite curs Structuri de date", Category.Notes, 20m, CreatedAt.AddHours(4),
                "Notite scrise de mana, cursurile din anul II."),
            Bicycle: Published("Bicicleta de oras", Category.Other, 300m, CreatedAt.AddHours(5)),
            DraftTextbook: Listing.Create(
                SellerId, "Manual Analiza matematica, editia 1", Category.Textbook, Condition.Good, 25m,
                CreatedAt.AddHours(6), description: "Editie mai veche, cu sublinieri.").Value);

        await SeedAsync(
            listings.AnalysisTextbook, listings.AnalysisWorkbook, listings.LenovoLaptop, listings.DellLaptop,
            listings.Notes, listings.Bicycle, listings.DraftTextbook);
        return listings;
    }

    private static Listing Published(
        string title, Category category, decimal price, DateTimeOffset createdAt, string? description = null)
    {
        var listing = Listing.Create(SellerId, title, category, Condition.Good, price, createdAt, description).Value;
        listing.Publish();
        return listing;
    }

    private async Task AssertListingCountAsync(int expected)
    {
        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(expected);
    }

    private sealed record RankingListings(
        Listing AnalysisTextbook,
        Listing AnalysisWorkbook,
        Listing LenovoLaptop,
        Listing DellLaptop,
        Listing Notes,
        Listing Bicycle,
        Listing DraftTextbook);
}

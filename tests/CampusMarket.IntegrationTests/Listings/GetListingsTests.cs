using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
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

    [Fact]
    public async Task Given_OneDraftAndOnePublished_When_GettingListings_Then_Returns200WithOnlyThePublishedOne()
    {
        // Arrange
        var draft = Listing.Create(SellerId, "Draft listing", Category.Textbook, Condition.Good, 30m, CreatedAt).Value;
        var published = Listing.Create(SellerId, "Published listing", Category.Textbook, Condition.Good, 40m, CreatedAt.AddHours(1)).Value;
        published.Publish();
        await SeedAsync(draft, published);

        // Act
        var response = await Client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(1);
        body.Count.Should().Be(1);
        body.Items.Select(i => i.Id).Should().Equal(published.Id);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task Given_ThreeListings_When_SearchingByText_Then_Returns200WithMatchingListings()
    {
        // Arrange
        var (oopTextbook, oopProblemSet, calculator) = await SeedThreeListingsAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/listings?search=OOP");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(2);
        body.Items.Select(i => i.Id).Should().Equal(oopProblemSet.Id, oopTextbook.Id);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_ThreeListings_When_FilteringByCategoryPriceAndSearch_Then_Returns200WithOnlyMatchingListing()
    {
        // Arrange
        var (oopTextbook, _, _) = await SeedThreeListingsAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/listings?category=Textbook&minPrice=20&maxPrice=100&search=oop");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(1);
        body.Items.Select(i => i.Id).Should().Equal(oopTextbook.Id);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_ThreeListings_When_FilteringByInclusivePriceRange_Then_Returns200WithOnlyMatchingListing()
    {
        // Arrange
        var (oopTextbook, _, _) = await SeedThreeListingsAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/listings?minPrice=40&maxPrice=40");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(1);
        body.Items.Select(i => i.Id).Should().Equal(oopTextbook.Id);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_ThreeListings_When_GettingListingsWithoutParameters_Then_Returns200WithAllListingsNewestFirst()
    {
        // Arrange
        var (oopTextbook, oopProblemSet, calculator) = await SeedThreeListingsAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(3);
        body.Count.Should().Be(3);
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(20);
        body.Items.Select(i => i.Id).Should().Equal(calculator.Id, oopProblemSet.Id, oopTextbook.Id);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_ThreeListings_When_RequestingSecondPage_Then_Returns200WithOldestListing()
    {
        // Arrange
        var (oopTextbook, _, _) = await SeedThreeListingsAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/listings?page=2&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Count.Should().Be(1);
        body.TotalCount.Should().Be(3);
        body.Page.Should().Be(2);
        body.PageSize.Should().Be(2);
        body.Items.Select(i => i.Id).Should().Equal(oopTextbook.Id);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_ThreeListings_When_RequestingPageZero_Then_Returns400InvalidPage()
    {
        // Arrange
        await SeedThreeListingsAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/listings?page=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listings.InvalidPage");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_ThreeListings_When_RequestingPageSizeAboveMax_Then_Returns400InvalidPageSize()
    {
        // Arrange
        await SeedThreeListingsAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/listings?pageSize=101");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listings.InvalidPageSize");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_ThreeListings_When_MinPriceGreaterThanMaxPrice_Then_Returns400InvalidPriceRange()
    {
        // Arrange
        await SeedThreeListingsAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/listings?minPrice=100&maxPrice=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listings.InvalidPriceRange");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_ThreeListings_When_CategoryCannotBeParsed_Then_Returns400()
    {
        // Arrange
        await SeedThreeListingsAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/listings?category=Abc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_GoodAndWornListings_When_FilteringByCondition_Then_Returns200WithOnlyGoodListing()
    {
        // Arrange
        var (good, _) = await SeedGoodAndWornListingsAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/listings?condition=Good");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(1);
        body.Count.Should().Be(1);
        body.Items.Select(i => i.Id).Should().Equal(good.Id);
        body.Items[0].Condition.Should().Be(Condition.Good);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task Given_GoodAndWornListings_When_ConditionCannotBeParsed_Then_Returns400()
    {
        // Arrange
        await SeedGoodAndWornListingsAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/listings?condition=Banana");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(2);
    }

    private async Task<(Listing good, Listing worn)> SeedGoodAndWornListingsAsync()
    {
        var good = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        var worn = Listing.Create(SellerId, "Culegere analiza", Category.Textbook, Condition.Worn, 20m, CreatedAt.AddHours(1)).Value;
        good.Publish();
        worn.Publish();
        await SeedAsync(good, worn);
        return (good, worn);
    }

    private async Task<(Listing oopTextbook, Listing oopProblemSet, Listing calculator)> SeedThreeListingsAsync()
    {
        var oopTextbook = Listing.Create(SellerId, "OOP textbook", Category.Textbook, Condition.Good, 40m, CreatedAt).Value;
        var oopProblemSet = Listing.Create(SellerId, "oop problem set", Category.Textbook, Condition.Good, 15m, CreatedAt.AddHours(1)).Value;
        var calculator = Listing.Create(SellerId, "Scientific calculator", Category.Electronics, Condition.New, 120m, CreatedAt.AddHours(2)).Value;
        oopTextbook.Publish();
        oopProblemSet.Publish();
        calculator.Publish();
        await SeedAsync(oopTextbook, oopProblemSet, calculator);
        return (oopTextbook, oopProblemSet, calculator);
    }
}

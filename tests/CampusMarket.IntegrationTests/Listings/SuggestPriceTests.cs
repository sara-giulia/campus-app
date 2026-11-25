using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.SuggestPrice;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class SuggestPriceTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Given_ValidLaptop_When_SuggestingPrice_Then_Returns200WithWholePriceAtLeastOne()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new SuggestPriceRequest(
            Title: "Laptop Lenovo ThinkPad T480, i5, 16GB RAM",
            Category: Category.Electronics,
            Condition: Condition.New);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/price-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SuggestPriceResponse>(Json);
        body!.SuggestedPrice.Should().BeGreaterThanOrEqualTo(1);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_LaptopAndNotes_When_SuggestingPrice_Then_LaptopIsMoreExpensiveThanNotes()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var laptop = new SuggestPriceRequest(
            Title: "Laptop Lenovo ThinkPad T480, i5, 16GB RAM",
            Category: Category.Electronics,
            Condition: Condition.New);
        var notes = new SuggestPriceRequest(
            Title: "Notite Analiza matematica anul 1",
            Category: Category.Notes,
            Condition: Condition.Worn);

        // Act
        var laptopResponse = await Client.PostAsJsonAsync("/api/v1/listings/price-suggestion", laptop, Json);
        var notesResponse = await Client.PostAsJsonAsync("/api/v1/listings/price-suggestion", notes, Json);

        // Assert
        laptopResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        notesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var laptopBody = await laptopResponse.Content.ReadFromJsonAsync<SuggestPriceResponse>(Json);
        var notesBody = await notesResponse.Content.ReadFromJsonAsync<SuggestPriceResponse>(Json);

        laptopBody!.SuggestedPrice.Should().BeGreaterThan(notesBody!.SuggestedPrice);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoToken_When_SuggestingPrice_Then_Returns401()
    {
        // Arrange
        var request = new SuggestPriceRequest(
            Title: "Laptop Lenovo ThinkPad T480, i5, 16GB RAM",
            Category: Category.Electronics,
            Condition: Condition.New);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/price-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/listings/price-suggestion");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyTitle_When_SuggestingPrice_Then_Returns400TitleEmpty()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new SuggestPriceRequest(
            Title: "",
            Category: Category.Electronics,
            Condition: Condition.New);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/price-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.TitleEmpty");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_DescriptionTooLong_When_SuggestingPrice_Then_Returns400DescriptionTooLong()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new SuggestPriceRequest(
            Title: "Laptop Lenovo ThinkPad T480, i5, 16GB RAM",
            Category: Category.Electronics,
            Condition: Condition.New,
            Description: new string('a', 1001));

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/price-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.DescriptionTooLong");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownConditionInJson_When_SuggestingPrice_Then_Returns400()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var json = """
            {
              "title": "Laptop Lenovo ThinkPad T480, i5, 16GB RAM",
              "category": "Electronics",
              "condition": "Used"
            }
            """;
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/listings/price-suggestion", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Code.Should().Be("Request.Invalid");
        problem.Instance.Should().Be("/api/v1/listings/price-suggestion");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoCategory_When_SuggestingPrice_Then_Returns400CategoryRequired()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var body = new { title = "Laptop Lenovo ThinkPad T480", condition = Condition.Good };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/price-suggestion", body, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.CategoryRequired");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoCondition_When_SuggestingPrice_Then_Returns400ConditionRequired()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var body = new { title = "Laptop Lenovo ThinkPad T480", category = Category.Electronics };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/price-suggestion", body, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.ConditionRequired");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }
}

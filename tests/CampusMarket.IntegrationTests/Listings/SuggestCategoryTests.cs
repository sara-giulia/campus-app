using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.SuggestCategory;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class SuggestCategoryTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Given_Laptop_When_SuggestingCategory_Then_Returns200Electronics()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new SuggestCategoryRequest(
            Title: "Laptop Lenovo ThinkPad T480, i5, 16GB RAM",
            Description: "Bateria tine 4 ore.");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/category-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SuggestCategoryResponse>(Json);
        body!.Category.Should().Be(Category.Electronics);
        body.Confidence.Should().BeInRange(0, 1);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_CourseNotes_When_SuggestingCategory_Then_Returns200Notes()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new SuggestCategoryRequest(Title: "Notite Analiza matematica anul 1");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/category-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SuggestCategoryResponse>(Json);
        body!.Category.Should().Be(Category.Notes);
        body.Confidence.Should().BeInRange(0, 1);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnrelatedTitle_When_SuggestingCategory_Then_Returns200WithNullCategory()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new SuggestCategoryRequest(
            Title: "Banana",
            Description: "Am de vandut o banana buna");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/category-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SuggestCategoryResponse>(Json);
        body!.Category.Should().BeNull();
        body.Confidence.Should().BeLessThan(SuggestCategoryHandler.MinConfidence);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_ConfidentNotesTitle_When_SuggestingCategory_Then_Returns200Notes()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new SuggestCategoryRequest(Title: "Notite Analiza matematica, an I");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/category-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SuggestCategoryResponse>(Json);
        body!.Category.Should().Be(Category.Notes);
        body.Confidence.Should().BeGreaterThanOrEqualTo(SuggestCategoryHandler.MinConfidence);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoToken_When_SuggestingCategory_Then_Returns401()
    {
        // Arrange
        var request = new SuggestCategoryRequest(Title: "Laptop Lenovo ThinkPad T480, i5, 16GB RAM");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/category-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/listings/category-suggestion");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyTitle_When_SuggestingCategory_Then_Returns400TitleEmpty()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new SuggestCategoryRequest(Title: "   ");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/category-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.TitleEmpty");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_DescriptionTooLong_When_SuggestingCategory_Then_Returns400DescriptionTooLong()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new SuggestCategoryRequest(
            Title: "Laptop Lenovo ThinkPad T480, i5, 16GB RAM",
            Description: new string('a', 1001));

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings/category-suggestion", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.DescriptionTooLong");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }
}

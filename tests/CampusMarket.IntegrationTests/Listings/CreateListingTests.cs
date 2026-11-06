using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.CreateListing;
using CampusMarket.Api.Features.Listings.GetListingById;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class CreateListingTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Given_ValidListing_When_CreatingListing_Then_Returns201AndSavesListing()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new CreateListingRequest(
            Title: "  Manual POO  ",
            Category: Category.Textbook,
            Condition: Condition.Good,
            Price: 50m);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CreateListingResponse>(Json);
        body!.Title.Should().Be("Manual POO");
        body.SellerId.Should().Be(SellerId);
        body.Category.Should().Be(Category.Textbook);
        body.Condition.Should().Be(Condition.Good);
        body.Price.Should().Be(50m);
        body.Status.Should().Be(ListingStatus.Draft);
        body.Version.Should().Be(1);
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/listings/{body.Id}");

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(body.Id);
        saved.SellerId.Should().Be(SellerId);
        saved.Title.Should().Be("Manual POO");
        saved.Category.Should().Be(Category.Textbook);
        saved.Condition.Should().Be(Condition.Good);
        saved.Price.Amount.Should().Be(50m);
        saved.Status.Should().Be(ListingStatus.Draft);
        saved.Version.Should().Be(1);
    }

    [Fact]
    public async Task Given_EmptyTitle_When_CreatingListing_Then_Returns400AndSavesNothing()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new CreateListingRequest("", Category.Textbook, Condition.Good, 50m);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.TitleEmpty");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_Description_When_CreatingListing_Then_Returns201WithDescriptionAndGetReturnsIt()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new CreateListingRequest(
            Title: "Manual POO",
            Category: Category.Textbook,
            Condition: Condition.Good,
            Price: 50m,
            Description: "Looks almost new, no highlights.");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CreateListingResponse>(Json);
        body!.Description.Should().Be("Looks almost new, no highlights.");

        var getResponse = await Client.GetAsync($"/api/v1/listings/{body.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadFromJsonAsync<GetListingByIdResponse>(Json);
        getBody!.Description.Should().Be("Looks almost new, no highlights.");

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Description.Should().Be("Looks almost new, no highlights.");
    }

    [Fact]
    public async Task Given_NoDescription_When_CreatingListing_Then_Returns201WithNullDescription()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new CreateListingRequest("Manual POO", Category.Textbook, Condition.Good, 50m);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CreateListingResponse>(Json);
        body!.Description.Should().BeNull();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Description.Should().BeNull();
    }

    [Fact]
    public async Task Given_DescriptionTooLong_When_CreatingListing_Then_Returns400AndSavesNothing()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var description = new string('a', 1001);
        var request = new CreateListingRequest(
            "Manual POO", Category.Textbook, Condition.Good, 50m, description);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.DescriptionTooLong");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoToken_When_CreatingListing_Then_Returns401AndSavesNothing()
    {
        // Arrange
        var request = new CreateListingRequest("Manual POO", Category.Textbook, Condition.Good, 50m);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/listings");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }
}

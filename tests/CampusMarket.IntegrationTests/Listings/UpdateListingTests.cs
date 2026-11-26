using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.TrainingExamples;
using CampusMarket.Api.Features.Listings.UpdateListing;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class UpdateListingTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ValidChanges_When_UpdatingListing_Then_Returns204AndSavesChanges()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(SellerId);
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Worn, 30m);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Title.Should().Be("Notite POO");
        saved.Category.Should().Be(Category.Notes);
        saved.Condition.Should().Be(Condition.Worn);
        saved.Price.Amount.Should().Be(30m);
        saved.Version.Should().Be(2);
    }

    [Fact]
    public async Task Given_ZeroPrice_When_UpdatingListing_Then_Returns400AndKeepsListing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(SellerId);
        var request = new UpdateListingRequest("Manual POO", Category.Textbook, Condition.Good, 0m);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Money.Invalid");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(listing);
    }

    [Fact]
    public async Task Given_NoToken_When_UpdatingListing_Then_Returns401AndKeepsListing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Worn, 30m);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(listing);
    }

    [Fact]
    public async Task Given_AnotherStudent_When_UpdatingListing_Then_Returns403AndKeepsListing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(OtherStudentId);
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Worn, 30m);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotOwner");
        error.Type.Should().Be(ErrorType.Forbidden);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(listing);
    }

    [Fact]
    public async Task Given_AdminWhoIsNotTheSeller_When_UpdatingListing_Then_Returns204AndSavesChanges()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(AdminId, "Admin");
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Worn, 30m);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.SellerId.Should().Be(SellerId);
        saved.Title.Should().Be("Notite POO");
        saved.Category.Should().Be(Category.Notes);
        saved.Condition.Should().Be(Condition.Worn);
        saved.Price.Amount.Should().Be(30m);
        saved.Version.Should().Be(2);
    }

    [Fact]
    public async Task Given_NewDescription_When_UpdatingListing_Then_Returns204AndSavesDescription()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(SellerId);
        var request = new UpdateListingRequest(
            "Manual POO", Category.Textbook, Condition.Good, 50m, "Second edition, barely used.");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Description.Should().Be("Second edition, barely used.");
        saved.Version.Should().Be(2);
    }

    [Fact]
    public async Task Given_UnknownId_When_UpdatingListing_Then_Returns404()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(OtherStudentId);
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Worn, 30m);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{Guid.NewGuid()}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(listing);
    }

    [Fact]
    public async Task Given_NoCategory_When_UpdatingListing_Then_Returns400CategoryRequiredAndKeepsListing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(SellerId);
        var body = new { title = "Notite POO", condition = Condition.Worn, price = 30m };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", body, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.CategoryRequired");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(listing);
    }

    [Fact]
    public async Task Given_NoCondition_When_UpdatingListing_Then_Returns400ConditionRequiredAndKeepsListing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(SellerId);
        var body = new { title = "Notite POO", category = Category.Notes, price = 30m };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", body, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.ConditionRequired");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(listing);
    }

    [Fact]
    public async Task Given_SellerChoosesAnotherCategoryThanSuggested_When_UpdatingListing_Then_Returns204AndRecordsCategoryExample()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(SellerId);
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Worn, 30m, SuggestedCategory: "Textbook");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var example = await QueryDbAsync(db => db.Set<TrainingExample>().AsNoTracking().SingleAsync());
        example.ListingId.Should().Be(listing.Id);
        example.Kind.Should().Be(TrainingExampleKind.Category);
        example.Label.Should().Be("Notes");
        example.Source.Should().Be(TrainingExampleSource.SellerChoice);
        example.Title.Should().Be("Notite POO");
        example.Description.Should().BeNull();
        example.CreatedBy.Should().Be(SellerId);
        example.UpdatedAt.Should().BeNull();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Category.Should().Be(Category.Notes);
        saved.Version.Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Books")]
    [InlineData("NOTES")]
    public async Task Given_MissingInvalidOrSameSuggestion_When_UpdatingListing_Then_Returns204AndRecordsNoExample(string? suggested)
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(SellerId);
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Worn, 30m, SuggestedCategory: suggested);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Title.Should().Be("Notite POO");
        saved.Category.Should().Be(Category.Notes);
        saved.Condition.Should().Be(Condition.Worn);
        saved.Price.Amount.Should().Be(30m);
        saved.Version.Should().Be(2);
        var examples = await QueryDbAsync(db => db.Set<TrainingExample>().CountAsync());
        examples.Should().Be(0);
    }

    [Fact]
    public async Task Given_ExampleFromFirstEdit_When_SellerEditsAgain_Then_RelabelsTheSameExample()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(SellerId);
        var first = await Client.PutAsJsonAsync(
            $"/api/v1/listings/{listing.Id}",
            new UpdateListingRequest("Notite POO", Category.Notes, Condition.Good, 50m, SuggestedCategory: "Textbook"), Json);
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var firstExample = await QueryDbAsync(db => db.Set<TrainingExample>().AsNoTracking().SingleAsync());
        var request = new UpdateListingRequest("Calculator POO", Category.Electronics, Condition.Good, 50m, SuggestedCategory: "Textbook");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var example = await QueryDbAsync(db => db.Set<TrainingExample>().AsNoTracking().SingleAsync());
        example.Id.Should().Be(firstExample.Id);
        example.Label.Should().Be("Electronics");
        example.Title.Should().Be("Calculator POO");
        example.CreatedAt.Should().Be(firstExample.CreatedAt);
        example.UpdatedAt.Should().NotBeNull();
        example.UpdatedAt!.Value.Should().BeOnOrAfter(firstExample.CreatedAt);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Category.Should().Be(Category.Electronics);
        saved.Version.Should().Be(3);
    }

    [Fact]
    public async Task Given_AdminEditWithSuggestion_When_UpdatingListing_Then_Returns204AndRecordsNoExample()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(AdminId, "Admin");
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Worn, 30m, SuggestedCategory: "Textbook");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Category.Should().Be(Category.Notes);
        saved.Version.Should().Be(2);
        var examples = await QueryDbAsync(db => db.Set<TrainingExample>().CountAsync());
        examples.Should().Be(0);
    }
}

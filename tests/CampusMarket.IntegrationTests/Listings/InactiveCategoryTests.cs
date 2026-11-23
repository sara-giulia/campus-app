using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Listings.CreateListing;
using CampusMarket.Api.Features.Listings.GetListingById;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.Api.Features.Listings.UpdateListing;
using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusMarket.IntegrationTests.Listings;

// Create, edit and publish when a category was deactivated by an Admin. Every test has its own
// database, so a category deactivated here is active again in every other test.
public sealed class InactiveCategoryTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_InactiveCategory_When_CreatingListing_Then_Returns400AndSavesNothing()
    {
        // Arrange
        await DeactivateAsync(Category.Electronics);
        AuthenticateAs(SellerId);
        var request = new CreateListingRequest("Calculator stiintific", Category.Electronics, Condition.Good, 120m);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.CategoryInactive");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_InactiveTargetCategory_When_MovingListingIntoIt_Then_Returns400AndKeepsListing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        await DeactivateAsync(Category.Notes);
        AuthenticateAs(SellerId);
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Worn, 30m);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.CategoryInactive");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(listing);
    }

    [Fact]
    public async Task Given_ListingAlreadyInInactiveCategory_When_UpdatingWithoutChangingCategory_Then_Returns204AndSavesChanges()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        await DeactivateAsync(Category.Textbook);
        AuthenticateAs(SellerId);
        var request = new UpdateListingRequest("Manual POO, editia 2", Category.Textbook, Condition.Worn, 45m);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Title.Should().Be("Manual POO, editia 2");
        saved.Category.Should().Be(Category.Textbook);
        saved.Condition.Should().Be(Condition.Worn);
        saved.Price.Amount.Should().Be(45m);
        saved.Version.Should().Be(2);
    }

    [Fact]
    public async Task Given_DraftWhoseCategoryBecameInactive_When_PublishingListing_Then_Returns400AndKeepsDraft()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        await DeactivateAsync(Category.Textbook);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/publish", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.CategoryInactive");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Draft);
        saved.Version.Should().Be(1);

        var reports = await QueryDbAsync(db => db.Set<Report>().CountAsync());
        reports.Should().Be(0);
    }

    [Fact]
    public async Task Given_ActiveListingWhoseCategoryBecameInactive_When_SearchingListings_Then_Returns200WithListing()
    {
        // Arrange
        var listing = await SeedActiveListingAsync();
        await DeactivateAsync(Category.Textbook);

        // Act
        var response = await Client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(listing.Id);
        body.TotalCount.Should().Be(1);

        await AssertListingAndCategoryUnchangedAsync(listing.Id);
    }

    [Fact]
    public async Task Given_ActiveListingWhoseCategoryBecameInactive_When_FilteringByThatCategory_Then_Returns200WithListing()
    {
        // Arrange
        var listing = await SeedActiveListingAsync();
        await DeactivateAsync(Category.Textbook);

        // Act
        var response = await Client.GetAsync("/api/v1/listings?category=Textbook");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(listing.Id);
        body.Items[0].Category.Should().Be(Category.Textbook);
        body.TotalCount.Should().Be(1);

        await AssertListingAndCategoryUnchangedAsync(listing.Id);
    }

    [Fact]
    public async Task Given_ActiveListingWhoseCategoryBecameInactive_When_GettingListingById_Then_Returns200()
    {
        // Arrange
        var listing = await SeedActiveListingAsync();
        await DeactivateAsync(Category.Textbook);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{listing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingByIdResponse>(Json);
        body!.Id.Should().Be(listing.Id);
        body.Title.Should().Be("Manual POO");
        body.Category.Should().Be(Category.Textbook);
        body.Status.Should().Be(ListingStatus.Active);
        body.Version.Should().Be(2);

        await AssertListingAndCategoryUnchangedAsync(listing.Id);
    }

    // Arrange: a published Textbook listing of the seller, saved straight into the database.
    private async Task<Listing> SeedActiveListingAsync()
    {
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(listing);
        return listing;
    }

    // Assert: the read touched nothing, the listing is still Active and the category still inactive.
    private async Task AssertListingAndCategoryUnchangedAsync(Guid listingId)
    {
        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(listingId);
        saved.Status.Should().Be(ListingStatus.Active);
        saved.Version.Should().Be(2);

        var category = await QueryDbAsync(db =>
            db.Set<ListingCategory>().AsNoTracking().SingleAsync(c => c.Key == Category.Textbook));
        category.IsActive.Should().BeFalse();
    }

    // Arrange: deactivates one category straight in this test's own database.
    private async Task DeactivateAsync(Category key)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        var all = await db.Set<ListingCategory>().ToListAsync();
        var category = all.Single(c => c.Key == key);

        category.Update(category.DisplayName, category.Description, isActive: false, all).IsSuccess.Should().BeTrue();

        await db.SaveChangesAsync();
    }
}

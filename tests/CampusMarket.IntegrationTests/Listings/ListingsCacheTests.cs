using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings;
using CampusMarket.Api.Features.Listings.CreateListing;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.Api.Features.Listings.UpdateListing;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.OutputCaching;
using CampusMarket.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusMarket.IntegrationTests.Listings;

// The output cache never stores a response to a request with an Authorization header,
// so every search here uses an anonymous client; only the changes use the logged-in Client.
public sealed class ListingsCacheTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_CachedSearch_When_ListingChangesInDatabase_Then_SameSearchStillReturnsOldTitle()
    {
        // Arrange
        await EmptyCacheAsync();
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(listing);
        using var anonymous = Factory.CreateClient();
        var before = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);
        before!.Items.Single().Title.Should().Be("Manual POO");

        // Act
        await RenameInDatabaseAsync(listing.Id, "Notite POO");
        var after = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);

        // Assert
        after!.Items.Single().Title.Should().Be("Manual POO");
        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Title.Should().Be("Notite POO");
    }

    [Fact]
    public async Task Given_CachedSearchWithDraft_When_PublishingListing_Then_SameSearchReturnsNewListing()
    {
        // Arrange
        await EmptyCacheAsync();
        using var anonymous = Factory.CreateClient();
        var empty = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings?search=Manual", Json);
        empty!.Items.Should().BeEmpty();
        AuthenticateAs(SellerId);
        var request = new CreateListingRequest("Manual POO", Category.Textbook, Condition.Good, 50m);
        var created = await Client.PostAsJsonAsync("/api/v1/listings", request, Json);
        var body = await created.Content.ReadFromJsonAsync<CreateListingResponse>(Json);
        var withDraft = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings?search=Manual", Json);
        withDraft!.Items.Should().BeEmpty();

        // Act
        var published = await Client.PostAsync($"/api/v1/listings/{body!.Id}/publish", content: null);
        var after = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings?search=Manual", Json);

        // Assert
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        published.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after!.Items.Select(i => i.Id).Should().Equal(body.Id);
        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public async Task Given_CachedSearch_When_UpdatingListingTitle_Then_SameSearchReturnsNewTitle()
    {
        // Arrange
        await EmptyCacheAsync();
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(listing);
        using var anonymous = Factory.CreateClient();
        var before = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);
        before!.Items.Single().Title.Should().Be("Manual POO");
        AuthenticateAs(SellerId);
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Good, 50m);

        // Act
        var updated = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);
        var after = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);

        // Assert
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after!.Items.Single().Title.Should().Be("Notite POO");
        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Title.Should().Be("Notite POO");
    }

    [Fact]
    public async Task Given_CachedSearch_When_ReservingListing_Then_SameSearchNoLongerReturnsIt()
    {
        // Arrange
        await EmptyCacheAsync();
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(buyer, listing);
        using var anonymous = Factory.CreateClient();
        var before = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);
        before!.Items.Select(i => i.Id).Should().Equal(listing.Id);
        AuthenticateAs(buyer.Id);

        // Act
        var reserved = await Client.PostAsync($"/api/v1/listings/{listing.Id}/reservations", content: null);
        var after = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);

        // Assert
        reserved.StatusCode.Should().Be(HttpStatusCode.Created);
        after!.Items.Should().BeEmpty();
        after.TotalCount.Should().Be(0);
        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Reserved);
    }

    // Every test starts with an empty cache, so the tests do not affect each other.
    private async Task EmptyCacheAsync()
    {
        var store = Factory.Services.GetRequiredService<IOutputCacheStore>();
        await store.EvictByTagAsync(ListingsCache.Tag, CancellationToken.None);
    }

    // Changes the row without any endpoint, so nothing clears the cache.
    private async Task RenameInDatabaseAsync(Guid id, string title)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        await db.Database.ExecuteSqlAsync($"UPDATE \"Listings\" SET \"Title\" = {title} WHERE \"Id\" = {id}");
    }
}

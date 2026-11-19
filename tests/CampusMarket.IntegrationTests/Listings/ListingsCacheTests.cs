using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings.CreateListing;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.Api.Features.Listings.UpdateListing;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class ListingsCacheTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_CachedSearch_When_CreatingAndPublishingListing_Then_SameSearchReturnsNewListing()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var before = await Client.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings?search=Manual", Json);
        before!.Items.Should().BeEmpty();
        var request = new CreateListingRequest("Manual POO", Category.Textbook, Condition.Good, 50m);

        // Act
        var created = await Client.PostAsJsonAsync("/api/v1/listings", request, Json);
        var body = await created.Content.ReadFromJsonAsync<CreateListingResponse>(Json);
        var published = await Client.PostAsync($"/api/v1/listings/{body!.Id}/publish", content: null);
        var after = await Client.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings?search=Manual", Json);

        // Assert
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        published.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after!.Items.Select(i => i.Id).Should().Equal(body.Id);
    }

    [Fact]
    public async Task Given_CachedSearch_When_UpdatingListingTitle_Then_SameSearchReturnsNewTitle()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(listing);
        AuthenticateAs(SellerId);
        var before = await Client.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);
        before!.Items.Single().Title.Should().Be("Manual POO");
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Good, 50m);

        // Act
        var updated = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);
        var after = await Client.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);

        // Assert
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after!.Items.Single().Title.Should().Be("Notite POO");
    }

    [Fact]
    public async Task Given_CachedSearch_When_ReservingListing_Then_SameSearchNoLongerReturnsIt()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);
        var before = await Client.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);
        before!.Items.Select(i => i.Id).Should().Equal(listing.Id);

        // Act
        var reserved = await Client.PostAsync($"/api/v1/listings/{listing.Id}/reservations", content: null);
        var after = await Client.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);

        // Assert
        reserved.StatusCode.Should().Be(HttpStatusCode.Created);
        after!.Items.Should().BeEmpty();
        after.TotalCount.Should().Be(0);
    }
}

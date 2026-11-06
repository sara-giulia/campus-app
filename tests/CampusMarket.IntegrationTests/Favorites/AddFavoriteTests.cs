using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Favorites.AddFavorite;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Favorites;

public sealed class AddFavoriteTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_PublishedListing_When_AddingFavorite_Then_Returns201AndSavesFavorite()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(member, listing);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/members/me/favorites/{listing.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<AddFavoriteResponse>(Json);
        body!.ListingId.Should().Be(listing.Id);
        body.Id.Should().NotBeEmpty();
        response.Headers.Location!.ToString().Should().EndWith("/api/v1/members/me/favorites");

        var saved = await QueryDbAsync(db => db.Set<Favorite>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(body.Id);
        saved.MemberId.Should().Be(member.Id);
        saved.ListingId.Should().Be(listing.Id);
    }

    [Fact]
    public async Task Given_ListingAlreadyFavorited_When_AddingFavorite_Then_Returns409AndKeepsOneFavorite()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        var favorite = Favorite.Create(member.Id, listing, CreatedAt.AddHours(1)).Value;
        await SeedAsync(member, listing, favorite);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/members/me/favorites/{listing.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Favorite.AlreadyExists");
        error.Type.Should().Be(ErrorType.Conflict);

        var saved = await QueryDbAsync(db => db.Set<Favorite>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(favorite);
    }

    [Fact]
    public async Task Given_DraftListing_When_AddingFavorite_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(member, listing);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/members/me/favorites/{listing.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Favorite.ListingNotAvailable");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Favorite>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_Seller_When_AddingOwnListingToFavorites_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(seller, listing);
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/members/me/favorites/{listing.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Favorite.OwnListing");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Favorite>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListing_When_AddingFavorite_Then_Returns404AndSavesNothing()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(member, listing);
        AuthenticateAs(member.Id);
        var unknownId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/api/v1/members/me/favorites/{unknownId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Favorite>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_MemberNotInDatabase_When_AddingFavorite_Then_Returns404AndSavesNothing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(listing);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.PostAsync($"/api/v1/members/me/favorites/{listing.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Favorite>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoToken_When_AddingFavorite_Then_Returns401AndSavesNothing()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(member, listing);

        // Act
        var response = await Client.PostAsync($"/api/v1/members/me/favorites/{listing.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/members/me/favorites/{listing.Id}");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Favorite>().CountAsync());
        count.Should().Be(0);
    }
}

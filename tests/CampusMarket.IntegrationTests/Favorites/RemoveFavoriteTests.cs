using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Favorites;

public sealed class RemoveFavoriteTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_SavedFavorite_When_RemovingFavorite_Then_Returns204AndRemovesFavorite()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var favorite = Favorite.Create(member.Id, listing, CreatedAt.AddHours(1)).Value;
        await SeedAsync(member, listing, favorite);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/members/me/favorites/{listing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var favoriteExists = await QueryDbAsync(db => db.Set<Favorite>().AnyAsync(f => f.Id == favorite.Id));
        favoriteExists.Should().BeFalse();

        var listingExists = await QueryDbAsync(db => db.Set<Listing>().AnyAsync(l => l.Id == listing.Id));
        listingExists.Should().BeTrue();
    }

    [Fact]
    public async Task Given_ListingNeverSaved_When_RemovingFavorite_Then_Returns404()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(member, listing);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/members/me/favorites/{listing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Favorite.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Favorite>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_AnotherMembersFavorite_When_RemovingFavorite_Then_Returns404AndKeepsFavorite()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var otherMember = Member.Create("Maria Ionescu", "maria@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var favorite = Favorite.Create(member.Id, listing, CreatedAt.AddHours(1)).Value;
        await SeedAsync(member, otherMember, listing, favorite);
        AuthenticateAs(otherMember.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/members/me/favorites/{listing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Favorite.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Favorite>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(favorite);
    }

    [Fact]
    public async Task Given_NoToken_When_RemovingFavorite_Then_Returns401AndKeepsFavorite()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var favorite = Favorite.Create(member.Id, listing, CreatedAt.AddHours(1)).Value;
        await SeedAsync(member, listing, favorite);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/members/me/favorites/{listing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/members/me/favorites/{listing.Id}");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Favorite>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(favorite);
    }

    [Fact]
    public async Task Given_FavoritedListing_When_SellerDeletesListing_Then_Returns204AndFavoriteIsGone()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var favorite = Favorite.Create(member.Id, listing, CreatedAt.AddHours(1)).Value;
        await SeedAsync(member, listing, favorite);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var listingExists = await QueryDbAsync(db => db.Set<Listing>().AnyAsync(l => l.Id == listing.Id));
        listingExists.Should().BeFalse();

        var favoriteExists = await QueryDbAsync(db => db.Set<Favorite>().AnyAsync(f => f.Id == favorite.Id));
        favoriteExists.Should().BeFalse();
    }
}

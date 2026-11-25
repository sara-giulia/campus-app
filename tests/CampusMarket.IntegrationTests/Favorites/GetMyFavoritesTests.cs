using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Favorites.GetMyFavorites;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Favorites;

public sealed class GetMyFavoritesTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_NoFavorites_When_GettingMyFavorites_Then_Returns200WithEmptyList()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/favorites");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyFavoritesResponse>(Json);
        body!.Count.Should().Be(0);
        body.TotalCount.Should().Be(0);
        body.Items.Should().BeEmpty();

        var count = await QueryDbAsync(db => db.Set<Favorite>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_TwoFavorites_When_GettingMyFavorites_Then_Returns200NewestFirst()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var textbook = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        textbook.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var calculator = Listing.Create(SellerId, "Calculator stiintific", Category.Electronics, Condition.New, 120m, CreatedAt).Value;
        calculator.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var olderFavorite = Favorite.Create(member.Id, textbook, CreatedAt.AddHours(1)).Value;
        var newerFavorite = Favorite.Create(member.Id, calculator, CreatedAt.AddHours(2)).Value;
        await SeedAsync(member, textbook, calculator, olderFavorite, newerFavorite);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/favorites");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyFavoritesResponse>(Json);
        body!.Count.Should().Be(2);
        body.TotalCount.Should().Be(2);
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(20);
        body.Items.Should().Equal(
            new MyFavoriteItem(calculator.Id, "Calculator stiintific", Category.Electronics, Condition.New, 120m,
                ListingStatus.Active, newerFavorite.CreatedAt),
            new MyFavoriteItem(textbook.Id, "Manual POO", Category.Textbook, Condition.Good, 50m,
                ListingStatus.Active, olderFavorite.CreatedAt));

        var count = await QueryDbAsync(db => db.Set<Favorite>().CountAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task Given_AnotherMembersFavorite_When_GettingMyFavorites_Then_ItIsNotReturned()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var otherMember = Member.Create("Maria Ionescu", "maria@student.ro", CreatedAt).Value;
        var textbook = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        textbook.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var calculator = Listing.Create(SellerId, "Calculator stiintific", Category.Electronics, Condition.New, 120m, CreatedAt).Value;
        calculator.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var myFavorite = Favorite.Create(member.Id, textbook, CreatedAt.AddHours(1)).Value;
        var otherFavorite = Favorite.Create(otherMember.Id, calculator, CreatedAt.AddHours(2)).Value;
        await SeedAsync(member, otherMember, textbook, calculator, myFavorite, otherFavorite);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/favorites");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyFavoritesResponse>(Json);
        body!.Count.Should().Be(1);
        body.TotalCount.Should().Be(1);
        body.Items.Select(i => i.ListingId).Should().Equal(textbook.Id);

        var count = await QueryDbAsync(db => db.Set<Favorite>().CountAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task Given_FavoriteOfHiddenListing_When_GettingMyFavorites_Then_ItIsNotReturned()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var favorite = Favorite.Create(member.Id, listing, CreatedAt.AddHours(1)).Value;
        listing.Hide();
        await SeedAsync(member, listing, favorite);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/favorites");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyFavoritesResponse>(Json);
        body!.Items.Should().BeEmpty();
        body.Count.Should().Be(0);
        body.TotalCount.Should().Be(0);

        var saved = await QueryDbAsync(db => db.Set<Favorite>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(favorite);
    }

    [Fact]
    public async Task Given_ThreeFavorites_When_GettingSecondPageOfTwo_Then_Returns200WithOldest()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var textbook = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        textbook.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var notes = Listing.Create(SellerId, "Notite Analiza", Category.Notes, Condition.Worn, 15m, CreatedAt).Value;
        notes.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var calculator = Listing.Create(SellerId, "Calculator stiintific", Category.Electronics, Condition.New, 120m, CreatedAt).Value;
        calculator.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var oldest = Favorite.Create(member.Id, textbook, CreatedAt.AddHours(1)).Value;
        var middle = Favorite.Create(member.Id, notes, CreatedAt.AddHours(2)).Value;
        var newest = Favorite.Create(member.Id, calculator, CreatedAt.AddHours(3)).Value;
        await SeedAsync(member, textbook, notes, calculator, oldest, middle, newest);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/favorites?page=2&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyFavoritesResponse>(Json);
        body!.Items.Select(i => i.ListingId).Should().Equal(textbook.Id);
        body.Count.Should().Be(1);
        body.TotalCount.Should().Be(3);
        body.Page.Should().Be(2);
        body.PageSize.Should().Be(2);

        var count = await QueryDbAsync(db => db.Set<Favorite>().CountAsync());
        count.Should().Be(3);
    }

    [Theory]
    [InlineData("?page=0", "Favorites.InvalidPage")]
    [InlineData("?pageSize=101", "Favorites.InvalidPageSize")]
    public async Task Given_InvalidPaging_When_GettingMyFavorites_Then_Returns400(string query, string expectedCode)
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var favorite = Favorite.Create(member.Id, listing, CreatedAt.AddHours(1)).Value;
        await SeedAsync(member, listing, favorite);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/me/favorites{query}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be(expectedCode);
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Favorite>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(favorite);
    }

    [Fact]
    public async Task Given_NoToken_When_GettingMyFavorites_Then_Returns401()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var favorite = Favorite.Create(member.Id, listing, CreatedAt.AddHours(1)).Value;
        await SeedAsync(member, listing, favorite);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/favorites");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/members/me/favorites");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Favorite>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(favorite);
    }
}

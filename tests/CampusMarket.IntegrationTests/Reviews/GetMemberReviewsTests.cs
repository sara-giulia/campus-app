using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reviews;
using CampusMarket.Api.Features.Reviews.GetMemberReviews;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Reviews;

public sealed class GetMemberReviewsTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_SellerWithTwoReviews_When_GettingReviews_Then_Returns200WithAverageNewestFirst()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var textbook = SoldListing(seller, buyer, "Manual POO");
        var calculator = SoldListing(seller, buyer, "Calculator stiintific");
        var olderReview = Review.Create(buyer.Id, textbook, 5, "Vanzator corect.", CreatedAt.AddHours(1)).Value;
        var newerReview = Review.Create(buyer.Id, calculator, 4, null, CreatedAt.AddHours(2)).Value;
        await SeedAsync(seller, buyer, textbook, calculator, olderReview, newerReview);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{seller.Id}/reviews");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMemberReviewsResponse>(Json);
        body!.MemberId.Should().Be(seller.Id);
        body.AverageRating.Should().Be(4.5);
        body.ReviewCount.Should().Be(2);
        body.Count.Should().Be(2);
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(20);
        body.Items.Should().Equal(
            new MemberReviewItem(newerReview.Id, calculator.Id, buyer.Id, 4, null, newerReview.CreatedAt),
            new MemberReviewItem(olderReview.Id, textbook.Id, buyer.Id, 5, "Vanzator corect.", olderReview.CreatedAt));

        var count = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task Given_SellerWithNoReviews_When_GettingReviews_Then_Returns200WithNullAverage()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        await SeedAsync(seller);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{seller.Id}/reviews");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMemberReviewsResponse>(Json);
        body!.MemberId.Should().Be(seller.Id);
        body.AverageRating.Should().BeNull();
        body.ReviewCount.Should().Be(0);
        body.Count.Should().Be(0);
        body.Items.Should().BeEmpty();

        var count = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_ReviewsOfAnotherSeller_When_GettingReviews_Then_TheyAreNotReturned()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var otherSeller = Member.Create("Maria Ionescu", "maria@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var textbook = SoldListing(seller, buyer, "Manual POO");
        var calculator = SoldListing(otherSeller, buyer, "Calculator stiintific");
        var myReview = Review.Create(buyer.Id, textbook, 5, "Vanzator corect.", CreatedAt.AddHours(1)).Value;
        var otherReview = Review.Create(buyer.Id, calculator, 2, "A intarziat.", CreatedAt.AddHours(2)).Value;
        await SeedAsync(seller, otherSeller, buyer, textbook, calculator, myReview, otherReview);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{seller.Id}/reviews");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMemberReviewsResponse>(Json);
        body!.ReviewCount.Should().Be(1);
        body.AverageRating.Should().Be(5);
        body.Items.Select(i => i.Id).Should().Equal(myReview.Id);

        var count = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task Given_ThreeReviews_When_GettingSecondPage_Then_Returns200WithOldestReview()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var textbook = SoldListing(seller, buyer, "Manual POO");
        var calculator = SoldListing(seller, buyer, "Calculator stiintific");
        var notes = SoldListing(seller, buyer, "Notite Analiza");
        var oldest = Review.Create(buyer.Id, textbook, 5, null, CreatedAt.AddHours(1)).Value;
        var middle = Review.Create(buyer.Id, calculator, 4, null, CreatedAt.AddHours(2)).Value;
        var newest = Review.Create(buyer.Id, notes, 3, null, CreatedAt.AddHours(3)).Value;
        await SeedAsync(seller, buyer, textbook, calculator, notes, oldest, middle, newest);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{seller.Id}/reviews?page=2&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMemberReviewsResponse>(Json);
        body!.Count.Should().Be(1);
        body.ReviewCount.Should().Be(3);
        body.Page.Should().Be(2);
        body.PageSize.Should().Be(2);
        body.Items.Select(i => i.Id).Should().Equal(oldest.Id);

        var count = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_PageZero_When_GettingReviews_Then_Returns400()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer, "Manual POO");
        var review = Review.Create(buyer.Id, listing, 5, "Vanzator corect.", CreatedAt.AddHours(1)).Value;
        await SeedAsync(seller, buyer, listing, review);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{seller.Id}/reviews?page=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Reviews.InvalidPage");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Review>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(review);
    }

    [Fact]
    public async Task Given_PageSizeTooLarge_When_GettingReviews_Then_Returns400()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer, "Manual POO");
        var review = Review.Create(buyer.Id, listing, 5, "Vanzator corect.", CreatedAt.AddHours(1)).Value;
        await SeedAsync(seller, buyer, listing, review);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{seller.Id}/reviews?pageSize=101");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Reviews.InvalidPageSize");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Review>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(review);
    }

    [Fact]
    public async Task Given_UnknownMember_When_GettingReviews_Then_Returns404()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer, "Manual POO");
        var review = Review.Create(buyer.Id, listing, 5, "Vanzator corect.", CreatedAt.AddHours(1)).Value;
        await SeedAsync(seller, buyer, listing, review);
        var unknownId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{unknownId}/reviews");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Review>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(review);
    }

    // A listing of this seller, published, reserved by the buyer and sold to them.
    private static Listing SoldListing(Member seller, Member buyer, string title)
    {
        var listing = Listing.Create(seller.Id, title, Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, CreatedAt).Value;
        listing.ConfirmSale(reservation.Id, CreatedAt);
        return listing;
    }
}

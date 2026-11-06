using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reviews;
using CampusMarket.Api.Features.Reviews.AddReview;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Reviews;

public sealed class AddReviewTests : IntegrationTestBase
{
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_BuyerOfSoldListing_When_AddingReview_Then_Returns201AndSavesReview()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new AddReviewRequest(5, "  Vanzator corect, recomand.  ");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reviews", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<AddReviewResponse>(Json);
        body!.Id.Should().NotBeEmpty();
        body.ListingId.Should().Be(listing.Id);
        body.SellerId.Should().Be(seller.Id);
        body.Rating.Should().Be(5);
        body.Comment.Should().Be("Vanzator corect, recomand.");
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/members/{seller.Id}/reviews");

        var saved = await QueryDbAsync(db => db.Set<Review>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(body.Id);
        saved.ListingId.Should().Be(listing.Id);
        saved.SellerId.Should().Be(seller.Id);
        saved.ReviewerId.Should().Be(buyer.Id);
        saved.Rating.Should().Be(5);
        saved.Comment.Should().Be("Vanzator corect, recomand.");
    }

    [Fact]
    public async Task Given_NoComment_When_AddingReview_Then_Returns201WithNullComment()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new AddReviewRequest(4);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reviews", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<AddReviewResponse>(Json);
        body!.Rating.Should().Be(4);
        body.Comment.Should().BeNull();

        var saved = await QueryDbAsync(db => db.Set<Review>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(body.Id);
        saved.Rating.Should().Be(4);
        saved.Comment.Should().BeNull();
    }

    [Fact]
    public async Task Given_ListingAlreadyReviewed_When_AddingReview_Then_Returns409AndKeepsOneReview()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        var review = Review.Create(buyer.Id, listing, 5, "Vanzator corect.", CreatedAt.AddHours(1)).Value;
        await SeedAsync(seller, buyer, listing, review);
        AuthenticateAs(buyer.Id);
        var request = new AddReviewRequest(3, "Alta parere");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reviews", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Review.AlreadyExists");
        error.Type.Should().Be(ErrorType.Conflict);

        var saved = await QueryDbAsync(db => db.Set<Review>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(review);
    }

    [Fact]
    public async Task Given_ReservedListing_When_AddingReview_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        listing.Reserve(buyer.Id, CreatedAt);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new AddReviewRequest(5, "Vanzator corect.");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reviews", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Review.SaleNotCompleted");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_RatingZero_When_AddingReview_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new AddReviewRequest(0, "Vanzator corect.");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reviews", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Review.InvalidRating");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_AnotherMember_When_AddingReview_Then_Returns403AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var otherMember = Member.Create("Maria Ionescu", "maria@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        await SeedAsync(seller, buyer, otherMember, listing);
        AuthenticateAs(otherMember.Id);
        var request = new AddReviewRequest(1, "Nu recomand.");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reviews", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Review.NotBuyer");
        error.Type.Should().Be(ErrorType.Forbidden);

        var count = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_Seller_When_ReviewingOwnSale_Then_Returns403AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(seller.Id);
        var request = new AddReviewRequest(5, "Sunt cel mai bun vanzator.");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reviews", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Review.NotBuyer");
        error.Type.Should().Be(ErrorType.Forbidden);

        var count = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListing_When_AddingReview_Then_Returns404AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new AddReviewRequest(5, "Vanzator corect.");
        var unknownId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{unknownId}/reviews", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_MemberNotInDatabase_When_AddingReview_Then_Returns404AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(OtherStudentId);
        var request = new AddReviewRequest(5, "Vanzator corect.");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reviews", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoToken_When_AddingReview_Then_Returns401AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        await SeedAsync(seller, buyer, listing);
        var request = new AddReviewRequest(5, "Vanzator corect.");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reviews", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/reviews");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_ReviewedListing_When_SellerDeletesListing_Then_Returns204AndReviewIsGone()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        var review = Review.Create(buyer.Id, listing, 5, "Vanzator corect.", CreatedAt.AddHours(1)).Value;
        await SeedAsync(seller, buyer, listing, review);
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var listingExists = await QueryDbAsync(db => db.Set<Listing>().AnyAsync(l => l.Id == listing.Id));
        listingExists.Should().BeFalse();

        var reviewExists = await QueryDbAsync(db => db.Set<Review>().AnyAsync(r => r.Id == review.Id));
        reviewExists.Should().BeFalse();
    }

    // A listing of this seller, published, reserved by the buyer and sold to them.
    private static Listing SoldListing(Member seller, Member buyer)
    {
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        var reservation = listing.Reserve(buyer.Id, CreatedAt).Value;
        listing.ConfirmSale(reservation.Id, CreatedAt);
        return listing;
    }
}

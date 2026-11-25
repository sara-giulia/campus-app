using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reviews;
using CampusMarket.Api.Features.Members.GetPublicProfile;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Members;

public sealed class PublicProfileTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_SellerWithActiveAndDraftListing_When_GettingProfile_Then_Returns200WithOnlyActiveListing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var active = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        active.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var draft = Listing.Create(seller.Id, "Calculator stiintific", Category.Electronics, Condition.New, 120m, CreatedAt.AddHours(1)).Value;
        await SeedAsync(seller, active, draft);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{seller.Id}/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("ion@student.ro");

        var body = await response.Content.ReadFromJsonAsync<GetPublicProfileResponse>(Json);
        body!.Id.Should().Be(seller.Id);
        body.Name.Should().Be("Ion Ionescu");
        body.MemberSince.Should().Be(CreatedAt);
        body.ReviewCount.Should().Be(0);
        body.AverageRating.Should().BeNull();
        body.ActiveListings.Should().Equal(new PublicListingItem(active.Id, "Manual POO", 50m));

        var savedMember = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        savedMember.Should().BeEquivalentTo(seller);
        var savedListings = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().ToListAsync());
        savedListings.Should().BeEquivalentTo(new[] { active, draft });
    }

    [Fact]
    public async Task Given_SellerWithReviewedSoldListing_When_GettingProfile_Then_Returns200WithRating()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var sold = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        sold.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = sold.Reserve(buyer.Id, CreatedAt).Value;
        sold.ConfirmSale(reservation.Id, CreatedAt);
        var review = Review.Create(buyer.Id, sold, 4, null, CreatedAt).Value;
        await SeedAsync(seller, buyer, sold, review);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{seller.Id}/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetPublicProfileResponse>(Json);
        body!.Id.Should().Be(seller.Id);
        body.AverageRating.Should().Be(4);
        body.ReviewCount.Should().Be(1);
        body.ActiveListings.Should().BeEmpty();

        var savedReview = await QueryDbAsync(db => db.Set<Review>().AsNoTracking().SingleAsync());
        savedReview.Should().BeEquivalentTo(review);
        var savedListing = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        savedListing.Status.Should().Be(ListingStatus.Sold);
        var memberCount = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        memberCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_UnknownId_When_GettingProfile_Then_Returns404()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var active = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        active.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(seller, active);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{Guid.NewGuid()}/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var savedMember = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        savedMember.Should().BeEquivalentTo(seller);
        var savedListing = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        savedListing.Should().BeEquivalentTo(active);
    }

    [Fact]
    public async Task Given_IdThatIsNotAGuid_When_GettingProfile_Then_Returns404()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        await SeedAsync(seller);

        // Act
        var response = await Client.GetAsync("/api/v1/members/abc/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var savedMember = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        savedMember.Should().BeEquivalentTo(seller);
    }
}

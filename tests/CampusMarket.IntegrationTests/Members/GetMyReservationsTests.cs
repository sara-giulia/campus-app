using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Reviews;
using CampusMarket.Api.Features.Members.GetMyReservations;
using CampusMarket.Api.Features.Reviews.AddReview;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Members;

public sealed class GetMyReservationsTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_NoToken_When_GettingMyReservations_Then_Returns401()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/members/me/reservations");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_BuyerWithTwoReservations_When_Getting_Then_Returns200WithBothNewestFirst()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;

        var olderListing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        olderListing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var olderReservation = olderListing.Reserve(buyer.Id, CreatedAt).Value;

        var newerListing = Listing.Create(SellerId, "Calculator stiintific", Category.Electronics, Condition.New, 120m, CreatedAt.AddHours(1)).Value;
        newerListing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var newerReservation = newerListing.Reserve(buyer.Id, CreatedAt.AddHours(1)).Value;

        await SeedAsync(buyer, olderListing, newerListing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        body!.Count.Should().Be(2);
        body.Items.Select(i => i.ReservationId).Should().Equal(newerReservation.Id, olderReservation.Id);
        body.Items[0].ListingTitle.Should().Be("Calculator stiintific");
        body.Items[0].Price.Should().Be(120m);
        body.Items[1].ListingTitle.Should().Be("Manual POO");
        body.Items[1].Price.Should().Be(50m);
    }

    [Fact]
    public async Task Given_AnotherStudent_When_Getting_Then_Returns200WithEmptyList()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        listing.Reserve(buyer.Id, CreatedAt);
        await SeedAsync(buyer, listing);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        body!.Count.Should().Be(0);
        body.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ReservationCreated49hAgo_When_Getting_Then_StatusExpiredInResponseButActiveInDb()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow.AddHours(-49)).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        body!.Count.Should().Be(1);
        body.Items.Single().Status.Should().Be(ReservationStatus.Expired);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Reservations.Single().Id.Should().Be(reservation.Id);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_SoldListingWithoutReview_When_Getting_Then_ReviewedIsFalse()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing("Manual POO", buyer, CreatedAt);
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        var item = body!.Items.Should().ContainSingle().Subject;
        item.ListingId.Should().Be(listing.Id);
        item.Reviewed.Should().BeFalse();
        await AssertDatabaseUnchangedAsync(expectedReviews: 0);
    }

    [Fact]
    public async Task Given_SoldListingReviewedByPost_When_Getting_Then_ReviewedIsTrueAndOtherFieldsUnchanged()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing("Manual POO", buyer, CreatedAt, seller.Id);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var before = await Client.GetFromJsonAsync<GetMyReservationsResponse>("/api/v1/members/me/reservations", Json);
        var post = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reviews", new AddReviewRequest(5), Json);
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        var item = body!.Items.Should().ContainSingle().Subject;
        item.Reviewed.Should().BeTrue();
        item.Should().BeEquivalentTo(before!.Items.Single(), o => o.Excluding(i => i.Reviewed));
        await AssertDatabaseUnchangedAsync(expectedReviews: 1, expectedNotifications: 1);
    }

    [Fact]
    public async Task Given_ReservedListing_When_Getting_Then_ReviewedIsFalse()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        listing.Reserve(buyer.Id, DateTimeOffset.UtcNow.ToMicroseconds());
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        var item = body!.Items.Should().ContainSingle().Subject;
        item.Status.Should().Be(ReservationStatus.Active);
        item.Reviewed.Should().BeFalse();
        await AssertDatabaseUnchangedAsync(expectedReviews: 0);
    }

    [Fact]
    public async Task Given_TwoSoldListingsOneReviewed_When_Getting_Then_EachItemHasItsOwnFlag()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var reviewedListing = SoldListing("Manual POO", buyer, CreatedAt, seller.Id);
        var plainListing = SoldListing("Calculator stiintific", buyer, CreatedAt.AddHours(1), seller.Id);
        var review = Review.Create(buyer.Id, reviewedListing, 4, null, CreatedAt.AddHours(2)).Value;
        await SeedAsync(seller, buyer, reviewedListing, plainListing, review);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        body!.Items.Should().HaveCount(2);
        body.Items.Single(i => i.ListingId == reviewedListing.Id).Reviewed.Should().BeTrue();
        body.Items.Single(i => i.ListingId == plainListing.Id).Reviewed.Should().BeFalse();
        await AssertDatabaseUnchangedAsync(expectedReviews: 1);
    }

    [Fact]
    public async Task Given_ReviewOfAnotherMember_When_Getting_Then_ReviewedIsFalseForThisBuyer()
    {
        // Arrange: this buyer reserved and cancelled; another member bought the listing and reviewed it.
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var other = Member.Create("Maria Ionescu", "maria@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var cancelled = listing.Reserve(buyer.Id, CreatedAt).Value;
        listing.CancelReservation(cancelled.Id);
        var sold = listing.Reserve(other.Id, CreatedAt.AddHours(1)).Value;
        listing.ConfirmSale(sold.Id, CreatedAt.AddHours(1));
        var review = Review.Create(other.Id, listing, 5, null, CreatedAt.AddHours(2)).Value;
        await SeedAsync(seller, buyer, other, listing, review);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        var item = body!.Items.Should().ContainSingle().Subject;
        item.ReservationId.Should().Be(cancelled.Id);
        item.Reviewed.Should().BeFalse();
        await AssertDatabaseUnchangedAsync(expectedReviews: 1);
    }

    private async Task AssertDatabaseUnchangedAsync(int expectedReviews, int expectedNotifications = 0)
    {
        var reviews = await QueryDbAsync(db => db.Set<Review>().CountAsync());
        reviews.Should().Be(expectedReviews);
        var notifications = await QueryDbAsync(db => db.Set<Notification>().CountAsync(n => n.Type == NotificationType.ReviewReceived));
        notifications.Should().Be(expectedNotifications);
    }

    // A listing of the fixed seller, published, reserved by the buyer and sold to them.
    private static Listing SoldListing(string title, Member buyer, DateTimeOffset createdAt, Guid? sellerId = null)
    {
        var listing = Listing.Create(sellerId ?? SellerId, title, Category.Textbook, Condition.Good, 50m, createdAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, createdAt).Value;
        listing.ConfirmSale(reservation.Id, createdAt);
        return listing;
    }

    [Fact]
    public async Task Given_ReservationsWithAndWithoutAgreedPrice_When_Getting_Then_ShowsAgreedPriceOrNull()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var agreedListing = Listing.Create(SellerId, "Calculator stiintific", Category.Electronics, Condition.New, 120m, CreatedAt.AddHours(1)).Value;
        agreedListing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var agreed = agreedListing.Reserve(buyer.Id, CreatedAt.AddHours(1), Money.Create(95.5m).Value).Value;
        var plainListing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        plainListing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var plain = plainListing.Reserve(buyer.Id, CreatedAt).Value;
        await SeedAsync(buyer, agreedListing, plainListing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        body!.Items.Select(i => i.ReservationId).Should().Equal(agreed.Id, plain.Id);
        body.Items[0].Price.Should().Be(120m);
        body.Items[0].AgreedPrice.Should().Be(95.5m);
        body.Items[1].Price.Should().Be(50m);
        body.Items[1].AgreedPrice.Should().BeNull();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).ToListAsync());
        saved.SelectMany(l => l.Reservations).Single(r => r.Id == agreed.Id).AgreedPrice.Should().Be(95.5m);
        saved.SelectMany(l => l.Reservations).Single(r => r.Id == plain.Id).AgreedPrice.Should().BeNull();
    }
}

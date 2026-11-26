using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Features.Notifications.GetMyNotifications;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Notifications;

public sealed class NotificationsTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_BuyerReservedListing_When_SellerGetsNotifications_Then_HasOneUnreadReservationCreated()
    {
        // Arrange
        var (_, buyer, listing) = await SeedSellerBuyerAndPublishedListingAsync();
        await ReserveAsBuyerAsync(buyer.Id, listing.Id);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyNotificationsResponse>(Json);
        body!.Count.Should().Be(1);
        body.TotalCount.Should().Be(1);
        body.UnreadCount.Should().Be(1);
        var item = body.Items.Single();
        item.Type.Should().Be(NotificationType.ReservationCreated);
        item.ListingId.Should().Be(listing.Id);
        item.ReadAt.Should().BeNull();

        var saved = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking().ToListAsync());
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(item.Id);
        saved[0].MemberId.Should().Be(SellerId);
        saved[0].ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_BuyerReservedListing_When_BuyerGetsNotifications_Then_ReturnsEmptyList()
    {
        // Arrange
        var (_, buyer, listing) = await SeedSellerBuyerAndPublishedListingAsync();
        await ReserveAsBuyerAsync(buyer.Id, listing.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyNotificationsResponse>(Json);
        body!.Items.Should().BeEmpty();
        body.Count.Should().Be(0);
        body.TotalCount.Should().Be(0);
        body.UnreadCount.Should().Be(0);

        var buyerNotifications = await QueryDbAsync(db => db.Set<Notification>()
            .CountAsync(n => n.MemberId == buyer.Id));
        buyerNotifications.Should().Be(0);
        var total = await QueryDbAsync(db => db.Set<Notification>().CountAsync());
        total.Should().Be(1);
    }

    [Fact]
    public async Task Given_SellerConfirmedSale_When_BuyerGetsNotifications_Then_HasOneListingSold()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        await SeedAsync(seller, buyer, listing);

        AuthenticateAs(seller.Id);
        var confirm = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm", content: null);
        confirm.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyNotificationsResponse>(Json);
        body!.Count.Should().Be(1);
        body.UnreadCount.Should().Be(1);
        var item = body.Items.Single();
        item.Type.Should().Be(NotificationType.ListingSold);
        item.ListingId.Should().Be(listing.Id);
        item.ReadAt.Should().BeNull();

        var saved = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking()
            .SingleAsync(n => n.MemberId == buyer.Id));
        saved.Id.Should().Be(item.Id);
        saved.Type.Should().Be(NotificationType.ListingSold);
    }

    [Fact]
    public async Task Given_MyUnreadNotification_When_MarkingAsRead_Then_Returns204AndSetsReadAt()
    {
        // Arrange
        var notification = BuildNotification(SellerId);
        await SeedAsync(notification);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync($"/api/v1/members/me/notifications/{notification.Id}/read", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking()
            .SingleAsync(n => n.Id == notification.Id));
        saved.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_NotificationMarkedAsRead_When_GettingNotifications_Then_UnreadCountIsZero()
    {
        // Arrange
        var notification = BuildNotification(SellerId);
        await SeedAsync(notification);
        AuthenticateAs(SellerId);
        var markRead = await Client.PostAsync($"/api/v1/members/me/notifications/{notification.Id}/read", content: null);
        markRead.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyNotificationsResponse>(Json);
        body!.UnreadCount.Should().Be(0);
        body.Count.Should().Be(1);
        body.Items.Single().Id.Should().Be(notification.Id);
        body.Items.Single().ReadAt.Should().NotBeNull();

        var saved = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking()
            .SingleAsync(n => n.Id == notification.Id));
        saved.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_SomeoneElsesNotification_When_MarkingAsRead_Then_Returns404AndLeavesItUnread()
    {
        // Arrange
        var notification = BuildNotification(SellerId);
        await SeedAsync(notification);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.PostAsync($"/api/v1/members/me/notifications/{notification.Id}/read", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Notification.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking()
            .SingleAsync(n => n.Id == notification.Id));
        saved.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_UnknownId_When_MarkingAsRead_Then_Returns404AndChangesNothing()
    {
        // Arrange
        var notification = BuildNotification(SellerId);
        await SeedAsync(notification);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync($"/api/v1/members/me/notifications/{Guid.NewGuid()}/read", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Notification.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking()
            .SingleAsync(n => n.Id == notification.Id));
        saved.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_TwoUnreadNotifications_When_MarkingAllAsRead_Then_Returns204AndOnlyMineAreRead()
    {
        // Arrange
        var first = BuildNotification(SellerId);
        var second = BuildNotification(SellerId, CreatedAt.AddHours(1));
        var othersNotification = BuildNotification(OtherStudentId);
        await SeedAsync(first, second, othersNotification);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync("/api/v1/members/me/notifications/read-all", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var mine = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking()
            .Where(n => n.MemberId == SellerId).ToListAsync());
        mine.Should().HaveCount(2);
        mine.Should().OnlyContain(n => n.ReadAt != null);

        var others = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking()
            .SingleAsync(n => n.Id == othersNotification.Id));
        others.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_NoToken_When_GettingNotifications_Then_Returns401()
    {
        // Arrange
        var notification = BuildNotification(SellerId);
        await SeedAsync(notification);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/notifications");

        // Assert
        await AssertUnauthorizedAsync(response, "/api/v1/members/me/notifications");
        await AssertStillUnreadAsync(notification.Id);
    }

    [Fact]
    public async Task Given_NoToken_When_MarkingAsRead_Then_Returns401()
    {
        // Arrange
        var notification = BuildNotification(SellerId);
        await SeedAsync(notification);

        // Act
        var response = await Client.PostAsync($"/api/v1/members/me/notifications/{notification.Id}/read", content: null);

        // Assert
        await AssertUnauthorizedAsync(response, $"/api/v1/members/me/notifications/{notification.Id}/read");
        await AssertStillUnreadAsync(notification.Id);
    }

    [Fact]
    public async Task Given_NoToken_When_MarkingAllAsRead_Then_Returns401()
    {
        // Arrange
        var notification = BuildNotification(SellerId);
        await SeedAsync(notification);

        // Act
        var response = await Client.PostAsync("/api/v1/members/me/notifications/read-all", content: null);

        // Assert
        await AssertUnauthorizedAsync(response, "/api/v1/members/me/notifications/read-all");
        await AssertStillUnreadAsync(notification.Id);
    }

    [Fact]
    public async Task Given_TwoNotifications_When_GettingNotifications_Then_ReturnsNewestFirst()
    {
        // Arrange
        var (seller, listing, older, newer) = await SeedSellerWithTwoNotificationsAsync();
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyNotificationsResponse>(Json);
        body!.Count.Should().Be(2);
        body.TotalCount.Should().Be(2);
        body.UnreadCount.Should().Be(2);
        body.Items.Select(i => i.Id).Should().Equal(newer.Id, older.Id);

        var first = body.Items[0];
        first.Type.Should().Be(NotificationType.ReservationCreated);
        first.Message.Should().Be("Maria Ionescu reserved Manual POO");
        first.ListingId.Should().Be(listing.Id);
        first.CreatedAt.Should().Be(newer.CreatedAt);
        first.ReadAt.Should().BeNull();

        var second = body.Items[1];
        second.Type.Should().Be(NotificationType.ReservationCreated);
        second.Message.Should().Be("Ana Popescu reserved Manual POO");
        second.ListingId.Should().Be(listing.Id);
        second.CreatedAt.Should().Be(older.CreatedAt);
        second.ReadAt.Should().BeNull();

        var saved = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking()
            .Where(n => n.MemberId == seller.Id).ToListAsync());
        saved.Should().HaveCount(2);
        saved.Should().OnlyContain(n => n.ReadAt == null);
    }

    [Fact]
    public async Task Given_NewerNotificationRead_When_GettingUnreadOnly_Then_ReturnsOnlyTheOlderOne()
    {
        // Arrange
        var (seller, _, older, newer) = await SeedSellerWithTwoNotificationsAsync();
        AuthenticateAs(seller.Id);
        var markRead = await Client.PostAsync($"/api/v1/members/me/notifications/{newer.Id}/read", content: null);
        markRead.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/notifications?unreadOnly=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyNotificationsResponse>(Json);
        body!.Count.Should().Be(1);
        body.TotalCount.Should().Be(1);
        body.UnreadCount.Should().Be(1);
        var item = body.Items.Single();
        item.Id.Should().Be(older.Id);
        item.ReadAt.Should().BeNull();

        var savedOlder = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking()
            .SingleAsync(n => n.Id == older.Id));
        savedOlder.ReadAt.Should().BeNull();
        var savedNewer = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking()
            .SingleAsync(n => n.Id == newer.Id));
        savedNewer.ReadAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("page=0", "Notifications.InvalidPage")]
    [InlineData("pageSize=101", "Notifications.InvalidPageSize")]
    public async Task Given_InvalidPaging_When_GettingNotifications_Then_Returns400(string queryString, string expectedCode)
    {
        // Arrange
        var notification = BuildNotification(SellerId);
        await SeedAsync(notification);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/me/notifications?{queryString}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be(expectedCode);
        error.Type.Should().Be(ErrorType.Validation);

        await AssertStillUnreadAsync(notification.Id);
    }

    private static Notification BuildNotification(Guid memberId, DateTimeOffset? createdAt = null) =>
        Notification.Create(memberId, NotificationType.ReservationCreated, "Ana Popescu reserved Manual POO",
            listingId: null, createdAt ?? CreatedAt).Value;

    private async Task<(Member Seller, Member Buyer, Listing Listing)> SeedSellerBuyerAndPublishedListingAsync()
    {
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(seller, buyer, listing);
        return (seller, buyer, listing);
    }

    // The older notification is seeded first, so the order of the response comes from CreatedAt.
    private async Task<(Member Seller, Listing Listing, Notification Older, Notification Newer)> SeedSellerWithTwoNotificationsAsync()
    {
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var older = Notification.Create(seller.Id, NotificationType.ReservationCreated,
            "Ana Popescu reserved Manual POO", listing.Id, CreatedAt.AddHours(1)).Value;
        var newer = Notification.Create(seller.Id, NotificationType.ReservationCreated,
            "Maria Ionescu reserved Manual POO", listing.Id, CreatedAt.AddHours(2)).Value;
        await SeedAsync(seller, listing, older, newer);
        return (seller, listing, older, newer);
    }

    // The notification is created by the reserve handler, so the reservation goes through the endpoint.
    private async Task ReserveAsBuyerAsync(Guid buyerId, Guid listingId)
    {
        AuthenticateAs(buyerId);
        var reserve = await Client.PostAsync($"/api/v1/listings/{listingId}/reservations", content: null);
        reserve.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response, string path)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be(path);
        problem.TraceId.Should().NotBeEmpty();
    }

    private async Task AssertStillUnreadAsync(Guid notificationId)
    {
        var saved = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking().ToListAsync());
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(notificationId);
        saved[0].ReadAt.Should().BeNull();
    }
}

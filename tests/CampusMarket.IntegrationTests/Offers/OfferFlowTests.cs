using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Conversations.GetConversation;
using CampusMarket.Api.Features.Members.GetMyReservations;
using CampusMarket.Api.Features.Offers;
using CampusMarket.Api.Features.Offers.AcceptOffer;
using CampusMarket.Api.Features.Offers.MakeOffer;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static CampusMarket.IntegrationTests.Offers.OfferTestData;

namespace CampusMarket.IntegrationTests.Offers;

public sealed class OfferFlowTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");
    private readonly Listing _listing;

    public OfferFlowTests()
    {
        _listing = ActiveListing(_seller.Id, price: 100m);
    }

    [Fact]
    public async Task Given_OfferCounterCounterAccept_When_SellerConfirmsSale_Then_EverythingUsesTheAgreedPrice()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);

        // Act: the buyer offers 90, the seller counters 95, the buyer counters 92, the seller accepts.
        AuthenticateAs(_buyer.Id);
        var made = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/offers", new OfferAmountRequest(90m), Json);
        made.StatusCode.Should().Be(HttpStatusCode.Created);
        var first = (await made.Content.ReadFromJsonAsync<MakeOfferResponse>(Json))!;

        AuthenticateAs(_seller.Id);
        var counter1 = await Client.PostAsJsonAsync($"/api/v1/offers/{first.Offer.Id}/counter", new OfferAmountRequest(95m), Json);
        counter1.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = (await counter1.Content.ReadFromJsonAsync<OfferResponse>(Json))!;

        AuthenticateAs(_buyer.Id);
        var counter2 = await Client.PostAsJsonAsync($"/api/v1/offers/{second.Id}/counter", new OfferAmountRequest(92m), Json);
        counter2.StatusCode.Should().Be(HttpStatusCode.OK);
        var third = (await counter2.Content.ReadFromJsonAsync<OfferResponse>(Json))!;

        AuthenticateAs(_seller.Id);
        var accepted = await Client.PostAsync($"/api/v1/offers/{third.Id}/accept", content: null);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        var acceptBody = (await accepted.Content.ReadFromJsonAsync<AcceptOfferResponse>(Json))!;

        // Assert: the listing is reserved at 92 and the chain of offers is correct.
        acceptBody.Reservation.AgreedPrice.Should().Be(92m);
        acceptBody.Reservation.BuyerId.Should().Be(_buyer.Id);
        var listing = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync());
        listing.Status.Should().Be(ListingStatus.Reserved);
        listing.Reservations.Should().ContainSingle().Which.AgreedPrice.Should().Be(92m);

        var offers = await QueryDbAsync(db => db.Set<Offer>().AsNoTracking().OrderBy(o => o.CreatedAt).ToListAsync());
        offers.Select(o => o.Id).Should().Equal(first.Offer.Id, second.Id, third.Id);
        offers.Select(o => o.Amount).Should().Equal(90m, 95m, 92m);
        offers.Select(o => o.Status).Should().Equal(OfferStatus.Countered, OfferStatus.Countered, OfferStatus.Accepted);
        offers.Select(o => o.ParentOfferId).Should().Equal(null, first.Offer.Id, second.Id);
        offers.Select(o => o.ProposedById).Should().Equal(_buyer.Id, _seller.Id, _buyer.Id);

        // Assert: both sides see the three offers in the conversation, oldest first.
        foreach (var member in new[] { _buyer, _seller })
        {
            AuthenticateAs(member.Id);
            var conversation = await Client.GetAsync($"/api/v1/conversations/{first.ConversationId}");
            conversation.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = (await conversation.Content.ReadFromJsonAsync<GetConversationResponse>(Json))!;
            body.Offers.Select(o => o.Id).Should().Equal(first.Offer.Id, second.Id, third.Id);
            body.Offers.Select(o => o.Status).Should().Equal(OfferStatus.Countered, OfferStatus.Countered, OfferStatus.Accepted);
            body.Offers.Select(o => o.Amount).Should().Equal(90m, 95m, 92m);
            body.Offers.Select(o => o.ParentOfferId).Should().Equal(null, first.Offer.Id, second.Id);
        }

        // Assert: the buyer sees the agreed price among the reservations.
        AuthenticateAs(_buyer.Id);
        var reservations = await Client.GetAsync("/api/v1/members/me/reservations");
        reservations.StatusCode.Should().Be(HttpStatusCode.OK);
        var reservationsBody = (await reservations.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json))!;
        var item = reservationsBody.Items.Should().ContainSingle().Subject;
        item.Price.Should().Be(100m);
        item.AgreedPrice.Should().Be(92m);

        // Act: the seller confirms the sale.
        AuthenticateAs(_seller.Id);
        var confirmed = await Client.PostAsync(
            $"/api/v1/listings/{_listing.Id}/reservations/{acceptBody.Reservation.Id}/confirm", content: null);

        // Assert: sold, and the sale record has the agreed price.
        confirmed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var sold = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync());
        sold.Status.Should().Be(ListingStatus.Sold);
        sold.Reservations.Single().Status.Should().Be(ReservationStatus.Completed);
        var record = await QueryDbAsync(db => db.Set<SaleRecord>().AsNoTracking().SingleAsync());
        record.ListingId.Should().Be(_listing.Id);
        record.SoldPrice.Should().Be(92m);

        // Assert: the notifications of every step went to the right member.
        var notifications = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking().OrderBy(n => n.CreatedAt).ToListAsync());
        var forSeller = notifications.Where(n => n.MemberId == _seller.Id).Select(n => n.Type).ToList();
        forSeller.Should().Equal(NotificationType.OfferReceived, NotificationType.OfferCountered, NotificationType.OfferAccepted);
        var forBuyer = notifications.Where(n => n.MemberId == _buyer.Id).Select(n => n.Type).ToList();
        forBuyer.Take(2).Should().Equal(NotificationType.OfferCountered, NotificationType.OfferAccepted);
        notifications.Where(n => n.Type is NotificationType.OfferReceived or NotificationType.OfferCountered
                or NotificationType.OfferAccepted or NotificationType.OfferRejected).Should().HaveCount(5);
        notifications.Single(n => n.Type == NotificationType.OfferReceived).Message.Should().Be("Ana Popescu offered 90 lei for Manual POO");
        notifications.Where(n => n.Type == NotificationType.OfferCountered).Select(n => n.Message)
            .Should().Equal("Ion Ionescu countered with 95 lei for Manual POO", "Ana Popescu countered with 92 lei for Manual POO");
    }

    [Fact]
    public async Task Given_ReservationWithoutOffer_When_SellerConfirmsSale_Then_SaleRecordHasTheListingPrice()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);
        var reserved = await Client.PostAsync($"/api/v1/listings/{_listing.Id}/reservations", content: null);
        reserved.StatusCode.Should().Be(HttpStatusCode.Created);
        var reservationId = (await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync()))
            .Reservations.Single().Id;

        // Act
        AuthenticateAs(_seller.Id);
        var confirmed = await Client.PostAsync($"/api/v1/listings/{_listing.Id}/reservations/{reservationId}/confirm", content: null);

        // Assert
        confirmed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var record = await QueryDbAsync(db => db.Set<SaleRecord>().AsNoTracking().SingleAsync());
        record.SoldPrice.Should().Be(100m);
        var reservation = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync());
        reservation.Reservations.Single().AgreedPrice.Should().BeNull();
    }
}

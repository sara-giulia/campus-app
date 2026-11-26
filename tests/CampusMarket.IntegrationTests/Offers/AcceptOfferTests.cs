using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Offers.AcceptOffer;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static CampusMarket.IntegrationTests.Offers.OfferTestData;

namespace CampusMarket.IntegrationTests.Offers;

public sealed class AcceptOfferTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");
    private readonly Member _otherBuyer = NewMember("Maria Ionescu", "maria@student.ro");
    private readonly Member _thirdBuyer = NewMember("Dan Popa", "dan@student.ro");
    private readonly Member _stranger = NewMember("Radu Vlad", "radu@student.ro");
    private readonly Listing _listing;
    private readonly Offer _offer;

    public AcceptOfferTests()
    {
        _listing = ActiveListing(_seller.Id);
        _offer = BuyerOffer(_listing, _buyer.Id, 90m);
    }

    private Task SeedBasicAsync(params object[] more) =>
        SeedAsync([_seller, _buyer, _otherBuyer, _thirdBuyer, _stranger, _listing, _offer, .. more]);

    private Task<HttpResponseMessage> AcceptAsync(Guid offerId) =>
        Client.PostAsync($"/api/v1/offers/{offerId}/accept", content: null);

    [Fact]
    public async Task Given_BuyerOffer_When_SellerAccepts_Then_Returns200AndReservesListingAtAgreedPrice()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);
        var before = DateTimeOffset.UtcNow.ToMicroseconds();

        // Act
        var response = await AcceptAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AcceptOfferResponse>(Json);
        body!.Offer.Id.Should().Be(_offer.Id);
        body.Offer.Status.Should().Be(OfferStatus.Accepted);
        body.Offer.RespondedAt.Should().NotBeNull();
        body.Reservation.ListingId.Should().Be(_listing.Id);
        body.Reservation.BuyerId.Should().Be(_buyer.Id);
        body.Reservation.AgreedPrice.Should().Be(90m);
        body.Reservation.Status.Should().Be(ReservationStatus.Active);

        var offer = await GetOfferAsync(_offer.Id);
        offer.Status.Should().Be(OfferStatus.Accepted);
        offer.RespondedAt.Should().NotBeNull();
        offer.RespondedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);

        var listing = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync());
        listing.Status.Should().Be(ListingStatus.Reserved);
        listing.Version.Should().Be(_listing.Version + 1);
        var reservation = listing.Reservations.Should().ContainSingle().Subject;
        reservation.Id.Should().Be(body.Reservation.Id);
        reservation.MemberId.Should().Be(_buyer.Id);
        reservation.AgreedPrice.Should().Be(90m);
        reservation.Status.Should().Be(ReservationStatus.Active);

        var notifications = await GetNotificationsAsync();
        notifications.Should().HaveCount(2);
        notifications.Should().OnlyContain(n => n.Type == NotificationType.OfferAccepted && n.ListingId == _listing.Id && n.ReadAt == null);
        notifications.Single(n => n.MemberId == _buyer.Id).Message.Should().Be("Ion Ionescu accepted your offer of 90 lei for Manual POO");
        notifications.Single(n => n.MemberId == _seller.Id).Message.Should().Be("You accepted the offer of 90 lei for Manual POO");
    }

    [Fact]
    public async Task Given_PendingOffersOfOtherBuyers_When_SellerAccepts_Then_TheyAreRejectedAndTheirBuyersNotified()
    {
        // Arrange
        var second = BuyerOffer(_listing, _otherBuyer.Id, 80m, minutes: 2);
        var third = BuyerOffer(_listing, _thirdBuyer.Id, 70m, minutes: 3);
        var alreadyRejected = BuyerOffer(_listing, _stranger.Id, 60m, minutes: 4);
        alreadyRejected.Reject(_seller.Id, CreatedAt.AddMinutes(5));
        await SeedBasicAsync(second, third, alreadyRejected);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await AcceptAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetOfferAsync(_offer.Id)).Status.Should().Be(OfferStatus.Accepted);
        foreach (var closed in new[] { second, third })
        {
            var saved = await GetOfferAsync(closed.Id);
            saved.Status.Should().Be(OfferStatus.Rejected);
            saved.RespondedAt.Should().NotBeNull();
        }

        var untouched = await GetOfferAsync(alreadyRejected.Id);
        untouched.Status.Should().Be(OfferStatus.Rejected);
        untouched.RespondedAt.Should().Be(CreatedAt.AddMinutes(5));

        var notifications = await GetNotificationsAsync();
        notifications.Should().HaveCount(4);
        notifications.Count(n => n.Type == NotificationType.OfferAccepted).Should().Be(2);
        var closedForSecond = notifications.Single(n => n.MemberId == _otherBuyer.Id);
        closedForSecond.Type.Should().Be(NotificationType.OfferRejected);
        closedForSecond.Message.Should().Be("The offer of 80 lei for Manual POO was closed: the listing was reserved by another buyer");
        var closedForThird = notifications.Single(n => n.MemberId == _thirdBuyer.Id);
        closedForThird.Type.Should().Be(NotificationType.OfferRejected);
        closedForThird.Message.Should().Be("The offer of 70 lei for Manual POO was closed: the listing was reserved by another buyer");
        notifications.Should().NotContain(n => n.MemberId == _stranger.Id);
    }

    [Fact]
    public async Task Given_SellerCounterOffer_When_BuyerAccepts_Then_Returns200WithCounterAmountAndNotifiesSeller()
    {
        // Arrange
        var counter = _offer.Counter(_seller.Id, _listing, 95m, CreatedAt.AddMinutes(2)).Value;
        await SeedBasicAsync(counter);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await AcceptAsync(counter.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AcceptOfferResponse>(Json);
        body!.Offer.Id.Should().Be(counter.Id);
        body.Offer.Status.Should().Be(OfferStatus.Accepted);
        body.Reservation.BuyerId.Should().Be(_buyer.Id);
        body.Reservation.AgreedPrice.Should().Be(95m);

        (await GetOfferAsync(counter.Id)).Status.Should().Be(OfferStatus.Accepted);
        (await GetOfferAsync(_offer.Id)).Status.Should().Be(OfferStatus.Countered);
        var listing = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync());
        listing.Status.Should().Be(ListingStatus.Reserved);
        listing.Reservations.Should().ContainSingle().Which.AgreedPrice.Should().Be(95m);

        var notifications = await GetNotificationsAsync();
        notifications.Should().HaveCount(2);
        notifications.Single(n => n.MemberId == _seller.Id).Message.Should().Be("Ana Popescu accepted your offer of 95 lei for Manual POO");
        notifications.Single(n => n.MemberId == _buyer.Id).Message.Should().Be("You accepted the offer of 95 lei for Manual POO");
    }

    [Fact]
    public async Task Given_Proposer_When_Accepting_Then_Returns403AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await AcceptAsync(_offer.Id);

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.Forbidden, "Offer.NotRecipient", ErrorType.Forbidden);
    }

    [Fact]
    public async Task Given_Stranger_When_Accepting_Then_Returns403AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_stranger.Id);

        // Act
        var response = await AcceptAsync(_offer.Id);

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.Forbidden, "Offer.NotParticipant", ErrorType.Forbidden);
    }

    [Fact]
    public async Task Given_OfferAlreadyAccepted_When_AcceptingAgain_Then_Returns400AndKeepsOneReservation()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);
        (await AcceptAsync(_offer.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var response = await AcceptAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Offer.NotPending");
        error.Type.Should().Be(ErrorType.Validation);
        var listing = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync());
        listing.Reservations.Should().ContainSingle();
        (await GetNotificationsAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_BuyerWithThreeActiveReservations_When_Accepting_Then_Returns400AndChangesNothing()
    {
        // Arrange
        var others = new List<Listing>();
        foreach (var title in new[] { "Calculus", "Physics", "Chemistry" })
        {
            var other = ActiveListing(_seller.Id, title);
            other.Reserve(_buyer.Id, CreatedAt);
            others.Add(other);
        }

        await SeedBasicAsync([.. others]);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await AcceptAsync(_offer.Id);

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.BadRequest, "Offer.BuyerReservationLimit", ErrorType.Validation);
    }

    [Fact]
    public async Task Given_ListingReservedMeanwhileByAnotherBuyer_When_Accepting_Then_Returns400AndKeepsOffersPending()
    {
        // Arrange
        var second = BuyerOffer(_listing, _otherBuyer.Id, 80m, minutes: 2);
        await SeedBasicAsync(second);
        AuthenticateAs(_otherBuyer.Id);
        (await Client.PostAsync($"/api/v1/listings/{_listing.Id}/reservations", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await AcceptAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Offer.ListingNotActive");
        error.Type.Should().Be(ErrorType.Validation);
        (await GetOfferAsync(_offer.Id)).Status.Should().Be(OfferStatus.Pending);
        (await GetOfferAsync(second.Id)).Status.Should().Be(OfferStatus.Pending);
        var listing = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync());
        listing.Reservations.Should().ContainSingle().Which.MemberId.Should().Be(_otherBuyer.Id);
        (await GetNotificationsAsync()).Should().NotContain(n => n.Type == NotificationType.OfferAccepted || n.Type == NotificationType.OfferRejected);
    }

    [Fact]
    public async Task Given_UnknownOffer_When_Accepting_Then_Returns404AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await AcceptAsync(Guid.NewGuid());

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.NotFound, "Offer.NotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task Given_NoToken_When_Accepting_Then_Returns401AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();

        // Act
        var response = await AcceptAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/offers/{_offer.Id}/accept");
        problem.TraceId.Should().NotBeEmpty();
        await AssertUnchangedAsync();
    }

    private async Task AssertErrorAndUnchangedAsync(HttpResponseMessage response, HttpStatusCode status, string code, ErrorType type)
    {
        response.StatusCode.Should().Be(status);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be(code);
        error.Type.Should().Be(type);
        await AssertUnchangedAsync();
    }

    // The offer is still Pending, the listing Active without reservations, and nobody was notified.
    private async Task AssertUnchangedAsync()
    {
        var offer = await GetOfferAsync(_offer.Id);
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
        var listing = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations)
            .SingleAsync(l => l.Id == _listing.Id));
        listing.Status.Should().Be(ListingStatus.Active);
        listing.Version.Should().Be(_listing.Version);
        listing.Reservations.Should().BeEmpty();
        (await GetNotificationsAsync()).Should().BeEmpty();
    }

    private Task<Offer> GetOfferAsync(Guid id) =>
        QueryDbAsync(db => db.Set<Offer>().AsNoTracking().SingleAsync(o => o.Id == id));

    private Task<List<Notification>> GetNotificationsAsync() =>
        QueryDbAsync(db => db.Set<Notification>().AsNoTracking().ToListAsync());
}

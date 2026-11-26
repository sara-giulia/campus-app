using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Offers;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static CampusMarket.IntegrationTests.Offers.OfferTestData;

namespace CampusMarket.IntegrationTests.Offers;

public sealed class RejectOfferTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");
    private readonly Member _stranger = NewMember("Radu Vlad", "radu@student.ro");
    private readonly Listing _listing;
    private readonly Offer _offer;

    public RejectOfferTests()
    {
        _listing = ActiveListing(_seller.Id);
        _offer = BuyerOffer(_listing, _buyer.Id, 90m);
    }

    private Task SeedBasicAsync(params object[] more) =>
        SeedAsync([_seller, _buyer, _stranger, _listing, _offer, .. more]);

    private Task<HttpResponseMessage> RejectAsync(Guid offerId) =>
        Client.PostAsync($"/api/v1/offers/{offerId}/reject", content: null);

    [Fact]
    public async Task Given_BuyerOffer_When_SellerRejects_Then_Returns200AndNotifiesBuyer()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);
        var before = DateTimeOffset.UtcNow.ToMicroseconds();

        // Act
        var response = await RejectAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OfferResponse>(Json);
        body!.Id.Should().Be(_offer.Id);
        body.Status.Should().Be(OfferStatus.Rejected);
        body.RespondedAt.Should().NotBeNull();

        var saved = await GetOfferAsync(_offer.Id);
        saved.Status.Should().Be(OfferStatus.Rejected);
        saved.RespondedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
        saved.Amount.Should().Be(90m);

        var notification = (await GetNotificationsAsync()).Should().ContainSingle().Subject;
        notification.Type.Should().Be(NotificationType.OfferRejected);
        notification.MemberId.Should().Be(_buyer.Id);
        notification.Message.Should().Be("Ion Ionescu rejected your offer of 90 lei for Manual POO");
        notification.ListingId.Should().Be(_listing.Id);
        notification.ReadAt.Should().BeNull();

        var listing = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync());
        listing.Status.Should().Be(ListingStatus.Active);
        listing.Reservations.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_SellerCounterOffer_When_BuyerRejects_Then_Returns200AndNotifiesSeller()
    {
        // Arrange
        var counter = _offer.Counter(_seller.Id, _listing, 95m, CreatedAt.AddMinutes(2)).Value;
        await SeedBasicAsync(counter);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await RejectAsync(counter.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetOfferAsync(counter.Id)).Status.Should().Be(OfferStatus.Rejected);
        (await GetOfferAsync(_offer.Id)).Status.Should().Be(OfferStatus.Countered);
        var notification = (await GetNotificationsAsync()).Should().ContainSingle().Subject;
        notification.Type.Should().Be(NotificationType.OfferRejected);
        notification.MemberId.Should().Be(_seller.Id);
        notification.Message.Should().Be("Ana Popescu rejected your offer of 95 lei for Manual POO");
    }

    [Fact]
    public async Task Given_Proposer_When_Rejecting_Then_Returns403AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await RejectAsync(_offer.Id);

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.Forbidden, "Offer.NotRecipient", ErrorType.Forbidden);
    }

    [Fact]
    public async Task Given_Stranger_When_Rejecting_Then_Returns403AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_stranger.Id);

        // Act
        var response = await RejectAsync(_offer.Id);

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.Forbidden, "Offer.NotParticipant", ErrorType.Forbidden);
    }

    [Fact]
    public async Task Given_OfferAlreadyRejected_When_RejectingAgain_Then_Returns400AndKeepsOneNotification()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);
        (await RejectAsync(_offer.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        var respondedAt = (await GetOfferAsync(_offer.Id)).RespondedAt;

        // Act
        var response = await RejectAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Offer.NotPending");
        error.Type.Should().Be(ErrorType.Validation);
        (await GetOfferAsync(_offer.Id)).RespondedAt.Should().Be(respondedAt);
        (await GetNotificationsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Given_UnknownOffer_When_Rejecting_Then_Returns404AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await RejectAsync(Guid.NewGuid());

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.NotFound, "Offer.NotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task Given_NoToken_When_Rejecting_Then_Returns401AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();

        // Act
        var response = await RejectAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/offers/{_offer.Id}/reject");
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

    private async Task AssertUnchangedAsync()
    {
        var offer = await GetOfferAsync(_offer.Id);
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
        (await GetNotificationsAsync()).Should().BeEmpty();
    }

    private Task<Offer> GetOfferAsync(Guid id) =>
        QueryDbAsync(db => db.Set<Offer>().AsNoTracking().SingleAsync(o => o.Id == id));

    private Task<List<Notification>> GetNotificationsAsync() =>
        QueryDbAsync(db => db.Set<Notification>().AsNoTracking().ToListAsync());
}

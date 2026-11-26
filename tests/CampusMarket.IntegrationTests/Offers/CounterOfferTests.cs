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

public sealed class CounterOfferTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");
    private readonly Member _stranger = NewMember("Radu Vlad", "radu@student.ro");
    private readonly Listing _listing;
    private readonly Offer _offer;

    public CounterOfferTests()
    {
        _listing = ActiveListing(_seller.Id);
        _offer = BuyerOffer(_listing, _buyer.Id, 90m);
    }

    private Task SeedBasicAsync(params object[] more) =>
        SeedAsync([_seller, _buyer, _stranger, _listing, _offer, .. more]);

    private Task<HttpResponseMessage> CounterAsync(Guid offerId, OfferAmountRequest request) =>
        Client.PostAsJsonAsync($"/api/v1/offers/{offerId}/counter", request, Json);

    [Fact]
    public async Task Given_BuyerOffer_When_SellerCounters_Then_Returns200AndSavesNewPendingOffer()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);
        var before = DateTimeOffset.UtcNow.ToMicroseconds();

        // Act
        var response = await CounterAsync(_offer.Id, new OfferAmountRequest(95m));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OfferResponse>(Json);
        body!.Id.Should().NotBe(_offer.Id);
        body.ListingId.Should().Be(_listing.Id);
        body.BuyerId.Should().Be(_buyer.Id);
        body.SellerId.Should().Be(_seller.Id);
        body.ProposedById.Should().Be(_seller.Id);
        body.Amount.Should().Be(95m);
        body.Status.Should().Be(OfferStatus.Pending);
        body.ParentOfferId.Should().Be(_offer.Id);
        body.RespondedAt.Should().BeNull();

        var old = await GetOfferAsync(_offer.Id);
        old.Status.Should().Be(OfferStatus.Countered);
        old.RespondedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
        var counter = await GetOfferAsync(body.Id);
        counter.Status.Should().Be(OfferStatus.Pending);
        counter.Amount.Should().Be(95m);
        counter.ProposedById.Should().Be(_seller.Id);
        counter.ParentOfferId.Should().Be(_offer.Id);
        counter.RespondedAt.Should().BeNull();
        counter.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
        (await QueryDbAsync(db => db.Set<Offer>().CountAsync())).Should().Be(2);

        var notification = (await GetNotificationsAsync()).Should().ContainSingle().Subject;
        notification.Type.Should().Be(NotificationType.OfferCountered);
        notification.MemberId.Should().Be(_buyer.Id);
        notification.Message.Should().Be("Ion Ionescu countered with 95 lei for Manual POO");
        notification.ListingId.Should().Be(_listing.Id);
        notification.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_SellerCounterOffer_When_BuyerCounters_Then_Returns200AndNotifiesSeller()
    {
        // Arrange
        var sellerCounter = _offer.Counter(_seller.Id, _listing, 95m, CreatedAt.AddMinutes(2)).Value;
        await SeedBasicAsync(sellerCounter);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await CounterAsync(sellerCounter.Id, new OfferAmountRequest(92.5m));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OfferResponse>(Json);
        body!.ProposedById.Should().Be(_buyer.Id);
        body.ParentOfferId.Should().Be(sellerCounter.Id);
        body.Amount.Should().Be(92.5m);
        body.Status.Should().Be(OfferStatus.Pending);

        (await GetOfferAsync(sellerCounter.Id)).Status.Should().Be(OfferStatus.Countered);
        (await GetOfferAsync(_offer.Id)).Status.Should().Be(OfferStatus.Countered);
        (await QueryDbAsync(db => db.Set<Offer>().CountAsync())).Should().Be(3);
        var notification = (await GetNotificationsAsync()).Should().ContainSingle().Subject;
        notification.Type.Should().Be(NotificationType.OfferCountered);
        notification.MemberId.Should().Be(_seller.Id);
        notification.Message.Should().Be("Ana Popescu countered with 92.5 lei for Manual POO");
    }

    [Fact]
    public async Task Given_Proposer_When_Countering_Then_Returns403AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await CounterAsync(_offer.Id, new OfferAmountRequest(80m));

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.Forbidden, "Offer.NotRecipient", ErrorType.Forbidden);
    }

    [Fact]
    public async Task Given_Stranger_When_Countering_Then_Returns403AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_stranger.Id);

        // Act
        var response = await CounterAsync(_offer.Id, new OfferAmountRequest(80m));

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.Forbidden, "Offer.NotParticipant", ErrorType.Forbidden);
    }

    [Fact]
    public async Task Given_RejectedOffer_When_Countering_Then_Returns400AndAddsNothing()
    {
        // Arrange
        _offer.Reject(_seller.Id, CreatedAt.AddMinutes(2));
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await CounterAsync(_offer.Id, new OfferAmountRequest(95m));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Offer.NotPending");
        error.Type.Should().Be(ErrorType.Validation);
        (await GetOfferAsync(_offer.Id)).Status.Should().Be(OfferStatus.Rejected);
        (await QueryDbAsync(db => db.Set<Offer>().CountAsync())).Should().Be(1);
        (await GetNotificationsAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Given_AmountNotPositive_When_Countering_Then_Returns400AndChangesNothing(int amount)
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await CounterAsync(_offer.Id, new OfferAmountRequest(amount));

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.BadRequest, "Offer.AmountInvalid", ErrorType.Validation);
    }

    [Fact]
    public async Task Given_MissingAmount_When_Countering_Then_Returns400AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await CounterAsync(_offer.Id, new OfferAmountRequest(null));

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.BadRequest, "Offer.AmountInvalid", ErrorType.Validation);
    }

    [Fact]
    public async Task Given_AmountAboveMaximum_When_Countering_Then_Returns400AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await CounterAsync(_offer.Id, new OfferAmountRequest(Money.MaxAmount + 0.01m));

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.BadRequest, "Offer.AmountTooHigh", ErrorType.Validation);
    }

    [Fact]
    public async Task Given_ListingReservedMeanwhile_When_Countering_Then_Returns400AndChangesNothing()
    {
        // Arrange
        _listing.Reserve(_stranger.Id, CreatedAt.AddMinutes(2));
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await CounterAsync(_offer.Id, new OfferAmountRequest(95m));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Offer.ListingNotActive");
        error.Type.Should().Be(ErrorType.Validation);
        await AssertOfferUnchangedAsync();
    }

    [Fact]
    public async Task Given_UnknownOffer_When_Countering_Then_Returns404AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await CounterAsync(Guid.NewGuid(), new OfferAmountRequest(95m));

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.NotFound, "Offer.NotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task Given_NoToken_When_Countering_Then_Returns401AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();

        // Act
        var response = await CounterAsync(_offer.Id, new OfferAmountRequest(95m));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/offers/{_offer.Id}/counter");
        problem.TraceId.Should().NotBeEmpty();
        await AssertOfferUnchangedAsync();
    }

    private async Task AssertErrorAndUnchangedAsync(HttpResponseMessage response, HttpStatusCode status, string code, ErrorType type)
    {
        response.StatusCode.Should().Be(status);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be(code);
        error.Type.Should().Be(type);
        await AssertOfferUnchangedAsync();
    }

    // Still the one Pending offer, nothing added, nobody notified.
    private async Task AssertOfferUnchangedAsync()
    {
        var offer = await GetOfferAsync(_offer.Id);
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
        (await QueryDbAsync(db => db.Set<Offer>().CountAsync())).Should().Be(1);
        (await GetNotificationsAsync()).Should().BeEmpty();
    }

    private Task<Offer> GetOfferAsync(Guid id) =>
        QueryDbAsync(db => db.Set<Offer>().AsNoTracking().SingleAsync(o => o.Id == id));

    private Task<List<Notification>> GetNotificationsAsync() =>
        QueryDbAsync(db => db.Set<Notification>().AsNoTracking().ToListAsync());
}

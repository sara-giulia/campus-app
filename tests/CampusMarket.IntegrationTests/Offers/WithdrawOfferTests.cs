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

public sealed class WithdrawOfferTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");
    private readonly Member _stranger = NewMember("Radu Vlad", "radu@student.ro");
    private readonly Listing _listing;
    private readonly Offer _offer;

    public WithdrawOfferTests()
    {
        _listing = ActiveListing(_seller.Id);
        _offer = BuyerOffer(_listing, _buyer.Id, 90m);
    }

    private Task SeedBasicAsync(params object[] more) =>
        SeedAsync([_seller, _buyer, _stranger, _listing, _offer, .. more]);

    private Task<HttpResponseMessage> WithdrawAsync(Guid offerId) =>
        Client.PostAsync($"/api/v1/offers/{offerId}/withdraw", content: null);

    [Fact]
    public async Task Given_PendingOffer_When_BuyerWithdraws_Then_Returns200AndCreatesNoNotification()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_buyer.Id);
        var before = DateTimeOffset.UtcNow.ToMicroseconds();

        // Act
        var response = await WithdrawAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OfferResponse>(Json);
        body!.Id.Should().Be(_offer.Id);
        body.Status.Should().Be(OfferStatus.Withdrawn);
        body.RespondedAt.Should().NotBeNull();

        var saved = await GetOfferAsync(_offer.Id);
        saved.Status.Should().Be(OfferStatus.Withdrawn);
        saved.RespondedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
        saved.Amount.Should().Be(90m);
        (await GetNotificationsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Seller_When_Withdrawing_Then_Returns403AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await WithdrawAsync(_offer.Id);

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.Forbidden, "Offer.NotBuyer", ErrorType.Forbidden);
    }

    [Fact]
    public async Task Given_Stranger_When_Withdrawing_Then_Returns403AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_stranger.Id);

        // Act
        var response = await WithdrawAsync(_offer.Id);

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.Forbidden, "Offer.NotParticipant", ErrorType.Forbidden);
    }

    [Fact]
    public async Task Given_SellerCounterOffer_When_BuyerWithdraws_Then_Returns400AndKeepsItPending()
    {
        // Arrange
        var counter = _offer.Counter(_seller.Id, _listing, 95m, CreatedAt.AddMinutes(2)).Value;
        await SeedBasicAsync(counter);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await WithdrawAsync(counter.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Offer.NotOwnOffer");
        error.Type.Should().Be(ErrorType.Validation);
        var saved = await GetOfferAsync(counter.Id);
        saved.Status.Should().Be(OfferStatus.Pending);
        saved.RespondedAt.Should().BeNull();
        (await GetNotificationsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_OfferAlreadyWithdrawn_When_WithdrawingAgain_Then_Returns400AndKeepsRespondedAt()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_buyer.Id);
        (await WithdrawAsync(_offer.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        var respondedAt = (await GetOfferAsync(_offer.Id)).RespondedAt;

        // Act
        var response = await WithdrawAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Offer.NotPending");
        error.Type.Should().Be(ErrorType.Validation);
        var saved = await GetOfferAsync(_offer.Id);
        saved.Status.Should().Be(OfferStatus.Withdrawn);
        saved.RespondedAt.Should().Be(respondedAt);
    }

    [Fact]
    public async Task Given_RejectedOffer_When_BuyerWithdraws_Then_Returns400AndKeepsRejected()
    {
        // Arrange
        _offer.Reject(_seller.Id, CreatedAt.AddMinutes(2));
        await SeedBasicAsync();
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await WithdrawAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Offer.NotPending");
        error.Type.Should().Be(ErrorType.Validation);
        var saved = await GetOfferAsync(_offer.Id);
        saved.Status.Should().Be(OfferStatus.Rejected);
        saved.RespondedAt.Should().Be(CreatedAt.AddMinutes(2));
    }

    [Fact]
    public async Task Given_UnknownOffer_When_Withdrawing_Then_Returns404AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await WithdrawAsync(Guid.NewGuid());

        // Assert
        await AssertErrorAndUnchangedAsync(response, HttpStatusCode.NotFound, "Offer.NotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task Given_NoToken_When_Withdrawing_Then_Returns401AndChangesNothing()
    {
        // Arrange
        await SeedBasicAsync();

        // Act
        var response = await WithdrawAsync(_offer.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/offers/{_offer.Id}/withdraw");
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

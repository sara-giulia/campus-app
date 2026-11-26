using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Offers;
using CampusMarket.Api.Features.Offers.MakeOffer;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static CampusMarket.IntegrationTests.Offers.OfferTestData;

namespace CampusMarket.IntegrationTests.Offers;

public sealed class MakeOfferTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");
    private readonly Member _otherBuyer = NewMember("Maria Ionescu", "maria@student.ro");
    private readonly Listing _listing;

    public MakeOfferTests()
    {
        _listing = ActiveListing(_seller.Id);
    }

    private string Url => $"/api/v1/listings/{_listing.Id}/offers";

    [Fact]
    public async Task Given_ActiveListingWithoutConversation_When_BuyerMakesOffer_Then_Returns201AndSavesOfferNotificationAndConversation()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);
        var before = DateTimeOffset.UtcNow.ToMicroseconds();

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(90m), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MakeOfferResponse>(Json);
        body!.ConversationId.Should().NotBeEmpty();
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/conversations/{body.ConversationId}");
        body.Offer.ListingId.Should().Be(_listing.Id);
        body.Offer.BuyerId.Should().Be(_buyer.Id);
        body.Offer.SellerId.Should().Be(_seller.Id);
        body.Offer.ProposedById.Should().Be(_buyer.Id);
        body.Offer.Amount.Should().Be(90m);
        body.Offer.Status.Should().Be(OfferStatus.Pending);
        body.Offer.ParentOfferId.Should().BeNull();
        body.Offer.RespondedAt.Should().BeNull();

        var offer = await QueryDbAsync(db => db.Set<Offer>().AsNoTracking().SingleAsync());
        offer.Id.Should().Be(body.Offer.Id);
        offer.ListingId.Should().Be(_listing.Id);
        offer.BuyerId.Should().Be(_buyer.Id);
        offer.SellerId.Should().Be(_seller.Id);
        offer.ProposedById.Should().Be(_buyer.Id);
        offer.Amount.Should().Be(90m);
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.ParentOfferId.Should().BeNull();
        offer.RespondedAt.Should().BeNull();
        offer.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);

        var notification = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking().SingleAsync());
        notification.Type.Should().Be(NotificationType.OfferReceived);
        notification.MemberId.Should().Be(_seller.Id);
        notification.Message.Should().Be("Ana Popescu offered 90 lei for Manual POO");
        notification.ListingId.Should().Be(_listing.Id);
        notification.ReadAt.Should().BeNull();

        var conversation = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().Include(c => c.Messages).SingleAsync());
        conversation.Id.Should().Be(body.ConversationId);
        conversation.ListingId.Should().Be(_listing.Id);
        conversation.BuyerId.Should().Be(_buyer.Id);
        conversation.SellerId.Should().Be(_seller.Id);
        conversation.Messages.Should().ContainSingle();
        conversation.Messages[0].SenderId.Should().Be(_buyer.Id);
        conversation.Messages[0].Text.Should().Be("Am făcut o ofertă de 90 lei.");
    }

    [Fact]
    public async Task Given_ExistingConversation_When_BuyerMakesOffer_Then_ReusesItWithoutNewMessage()
    {
        // Arrange
        var conversation = Conversation.Start(_buyer.Id, _listing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(90m), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MakeOfferResponse>(Json);
        body!.ConversationId.Should().Be(conversation.Id);

        var saved = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().Include(c => c.Messages).SingleAsync());
        saved.Id.Should().Be(conversation.Id);
        saved.Messages.Should().ContainSingle();
        saved.Messages[0].Text.Should().Be("Mai e disponibil?");
        (await QueryDbAsync(db => db.Set<Offer>().CountAsync())).Should().Be(1);
        (await QueryDbAsync(db => db.Set<Notification>().CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task Given_PendingOfferOfSameBuyer_When_MakingAnotherOffer_Then_Returns409AndKeepsOneOffer()
    {
        // Arrange
        var first = BuyerOffer(_listing, _buyer.Id, 80m);
        await SeedAsync(_seller, _buyer, _listing, first);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(90m), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Offer.AlreadyPending");
        error.Type.Should().Be(ErrorType.Conflict);

        var saved = await QueryDbAsync(db => db.Set<Offer>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(first.Id);
        saved.Amount.Should().Be(80m);
        (await QueryDbAsync(db => db.Set<Notification>().CountAsync())).Should().Be(0);
        (await QueryDbAsync(db => db.Set<Conversation>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_PendingCounterOfferFromSeller_When_BuyerMakesOffer_Then_Returns409()
    {
        // Arrange
        var first = BuyerOffer(_listing, _buyer.Id, 80m);
        var counter = first.Counter(_seller.Id, _listing, 95m, CreatedAt.AddMinutes(2)).Value;
        await SeedAsync(_seller, _buyer, _listing, first, counter);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(90m), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Offer.AlreadyPending");
        error.Type.Should().Be(ErrorType.Conflict);
        (await QueryDbAsync(db => db.Set<Offer>().CountAsync())).Should().Be(2);
        (await QueryDbAsync(db => db.Set<Notification>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_PendingOfferOfAnotherBuyer_When_BuyerMakesOffer_Then_Returns201()
    {
        // Arrange
        var other = BuyerOffer(_listing, _otherBuyer.Id, 70m);
        await SeedAsync(_seller, _buyer, _otherBuyer, _listing, other);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(90m), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var offers = await QueryDbAsync(db => db.Set<Offer>().AsNoTracking().ToListAsync());
        offers.Should().HaveCount(2);
        offers.Should().ContainSingle(o => o.BuyerId == _buyer.Id && o.Status == OfferStatus.Pending && o.Amount == 90m);
        offers.Should().ContainSingle(o => o.Id == other.Id && o.Status == OfferStatus.Pending);
    }

    [Fact]
    public async Task Given_RejectedOfferOfSameBuyer_When_BuyerMakesNewOffer_Then_Returns201()
    {
        // Arrange
        var rejected = BuyerOffer(_listing, _buyer.Id, 60m);
        rejected.Reject(_seller.Id, CreatedAt.AddMinutes(2));
        await SeedAsync(_seller, _buyer, _listing, rejected);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(90m), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var offers = await QueryDbAsync(db => db.Set<Offer>().AsNoTracking().ToListAsync());
        offers.Should().HaveCount(2);
        offers.Single(o => o.Id == rejected.Id).Status.Should().Be(OfferStatus.Rejected);
        offers.Single(o => o.Id != rejected.Id).Status.Should().Be(OfferStatus.Pending);
    }

    [Fact]
    public async Task Given_UnknownListing_When_MakingOffer_Then_Returns404AndSavesNothing()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{Guid.NewGuid()}/offers", new OfferAmountRequest(90m), Json);

        // Assert
        await AssertErrorAndNothingSavedAsync(response, HttpStatusCode.NotFound, "Listing.NotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task Given_MemberNotInDatabase_When_MakingOffer_Then_Returns404AndSavesNothing()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(Guid.NewGuid());

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(90m), Json);

        // Assert
        await AssertErrorAndNothingSavedAsync(response, HttpStatusCode.NotFound, "Member.NotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task Given_Seller_When_MakingOfferOnOwnListing_Then_Returns400AndSavesNothing()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(90m), Json);

        // Assert
        await AssertErrorAndNothingSavedAsync(response, HttpStatusCode.BadRequest, "Offer.OwnListing", ErrorType.Validation);
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Hidden)]
    public async Task Given_ListingNotActive_When_MakingOffer_Then_Returns400AndSavesNothing(ListingStatus status)
    {
        // Arrange
        var listing = status == ListingStatus.Draft
            ? Listing.Create(_seller.Id, "Manual POO", Category.Textbook, Condition.Good, 100m, CreatedAt).Value
            : ActiveListing(_seller.Id);
        if (status == ListingStatus.Reserved)
            listing.Reserve(_otherBuyer.Id, CreatedAt);
        if (status == ListingStatus.Hidden)
            listing.Hide();
        listing.Status.Should().Be(status);
        await SeedAsync(_seller, _buyer, _otherBuyer, listing);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/offers", new OfferAmountRequest(90m), Json);

        // Assert
        await AssertErrorAndNothingSavedAsync(response, HttpStatusCode.BadRequest, "Offer.ListingNotActive", ErrorType.Validation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Given_AmountNotPositive_When_MakingOffer_Then_Returns400AndSavesNothing(int amount)
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(amount), Json);

        // Assert
        await AssertErrorAndNothingSavedAsync(response, HttpStatusCode.BadRequest, "Offer.AmountInvalid", ErrorType.Validation);
    }

    [Fact]
    public async Task Given_MissingAmount_When_MakingOffer_Then_Returns400AndSavesNothing()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync(Url, new { }, Json);

        // Assert
        await AssertErrorAndNothingSavedAsync(response, HttpStatusCode.BadRequest, "Offer.AmountInvalid", ErrorType.Validation);
    }

    [Fact]
    public async Task Given_AmountAboveMaximum_When_MakingOffer_Then_Returns400AndSavesNothing()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(Money.MaxAmount + 0.01m), Json);

        // Assert
        await AssertErrorAndNothingSavedAsync(response, HttpStatusCode.BadRequest, "Offer.AmountTooHigh", ErrorType.Validation);
    }

    [Fact]
    public async Task Given_MaximumAmount_When_MakingOffer_Then_Returns201()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(Money.MaxAmount), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var saved = await QueryDbAsync(db => db.Set<Offer>().AsNoTracking().SingleAsync());
        saved.Amount.Should().Be(Money.MaxAmount);
    }

    [Fact]
    public async Task Given_NoToken_When_MakingOffer_Then_Returns401AndSavesNothing()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);

        // Act
        var response = await Client.PostAsJsonAsync(Url, new OfferAmountRequest(90m), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be(Url);
        problem.TraceId.Should().NotBeEmpty();
        await AssertNothingSavedAsync();
    }

    private async Task AssertErrorAndNothingSavedAsync(
        HttpResponseMessage response, HttpStatusCode status, string code, ErrorType type)
    {
        response.StatusCode.Should().Be(status);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be(code);
        error.Type.Should().Be(type);
        await AssertNothingSavedAsync();
    }

    // No offer, no conversation and no notification: the failed request changed nothing.
    private async Task AssertNothingSavedAsync()
    {
        (await QueryDbAsync(db => db.Set<Offer>().CountAsync())).Should().Be(0);
        (await QueryDbAsync(db => db.Set<Conversation>().CountAsync())).Should().Be(0);
        (await QueryDbAsync(db => db.Set<Notification>().CountAsync())).Should().Be(0);
    }
}

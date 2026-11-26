using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Conversations;
using CampusMarket.Api.Features.Conversations.GetConversation;
using CampusMarket.Api.Features.Offers;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static CampusMarket.IntegrationTests.Offers.OfferTestData;

namespace CampusMarket.IntegrationTests.Blocks;

// A block works one way: the blocked member gets 403 Member.Blocked, the blocker is not restricted.
public sealed class BlockedActionsTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");
    private readonly Member _third = NewMember("Maria Ionescu", "maria@student.ro");
    private readonly Listing _listing;

    public BlockedActionsTests()
    {
        _listing = ActiveListing(_seller.Id);
    }

    private MemberBlock SellerBlocksBuyer() => MemberBlock.Create(_seller.Id, _buyer.Id, CreatedAt).Value;

    private static async Task AssertBlockedAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.Blocked");
        error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_BuyerMakesOffer_Then_Returns403AndSavesNothing()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing, SellerBlocksBuyer());
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/offers", new OfferAmountRequest(90m), Json);

        // Assert
        await AssertBlockedAsync(response);
        (await QueryDbAsync(db => db.Set<Offer>().CountAsync())).Should().Be(0);
        (await QueryDbAsync(db => db.Set<Conversation>().CountAsync())).Should().Be(0);
        (await QueryDbAsync(db => db.Set<Notification>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_BuyerStartsConversation_Then_Returns403AndSavesNothing()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing, SellerBlocksBuyer());
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/conversations", new SendMessageRequest("Mai e disponibil?"), Json);

        // Assert
        await AssertBlockedAsync(response);
        (await QueryDbAsync(db => db.Set<Conversation>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_SellerBlockedBuyerAndExistingConversation_When_BuyerWritesAgain_Then_Returns403AndAddsNoMessage()
    {
        // Arrange
        var conversation = Conversation.Start(_buyer.Id, _listing, "Salut", CreatedAt).Value;
        await SeedAsync(_seller, _buyer, _listing, conversation, SellerBlocksBuyer());
        AuthenticateAs(_buyer.Id);

        // Act
        var viaListing = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/conversations", new SendMessageRequest("Alo?"), Json);
        var viaConversation = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", new SendMessageRequest("Alo?"), Json);

        // Assert
        await AssertBlockedAsync(viaListing);
        await AssertBlockedAsync(viaConversation);
        var saved = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().Include(c => c.Messages).SingleAsync());
        saved.Messages.Should().ContainSingle().Which.Text.Should().Be("Salut");
    }

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_BuyerReserves_Then_Returns403AndListingStaysActive()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing, SellerBlocksBuyer());
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{_listing.Id}/reservations", content: null);

        // Assert
        await AssertBlockedAsync(response);
        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);
        saved.Reservations.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_PendingSellerCounterOfferAndSellerBlock_When_BuyerCounters_Then_Returns403AndOfferStaysPending()
    {
        // Arrange: seeded directly, because blocking through the API would have rejected the pending offer.
        var buyerOffer = BuyerOffer(_listing, _buyer.Id, 80m);
        var sellerCounter = buyerOffer.Counter(_seller.Id, _listing, 95m, CreatedAt.AddMinutes(2)).Value;
        await SeedAsync(_seller, _buyer, _listing, buyerOffer, sellerCounter, SellerBlocksBuyer());
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/offers/{sellerCounter.Id}/counter", new OfferAmountRequest(90m), Json);

        // Assert
        await AssertBlockedAsync(response);
        var offers = await QueryDbAsync(db => db.Set<Offer>().AsNoTracking().ToListAsync());
        offers.Should().HaveCount(2);
        offers.Single(o => o.Id == sellerCounter.Id).Status.Should().Be(OfferStatus.Pending);
        (await QueryDbAsync(db => db.Set<Notification>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_BlockerActs_Then_SellerCanStillWriteAndCounter()
    {
        // Arrange
        var conversation = Conversation.Start(_buyer.Id, _listing, "Salut", CreatedAt).Value;
        var buyerOffer = BuyerOffer(_listing, _buyer.Id, 80m);
        await SeedAsync(_seller, _buyer, _listing, conversation, buyerOffer, SellerBlocksBuyer());
        AuthenticateAs(_seller.Id);

        // Act
        var message = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", new SendMessageRequest("Te-am blocat."), Json);
        var counter = await Client.PostAsJsonAsync($"/api/v1/offers/{buyerOffer.Id}/counter", new OfferAmountRequest(95m), Json);

        // Assert
        message.StatusCode.Should().Be(HttpStatusCode.Created);
        counter.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().Include(c => c.Messages).SingleAsync());
        saved.Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_BuyerBlockedSeller_When_BuyerActs_Then_BlockerCanOfferWriteAndReserve()
    {
        // Arrange
        var secondListing = ActiveListing(_seller.Id, "Calculator");
        await SeedAsync(_seller, _buyer, _listing, secondListing, MemberBlock.Create(_buyer.Id, _seller.Id, CreatedAt).Value);
        AuthenticateAs(_buyer.Id);

        // Act
        var offer = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/offers", new OfferAmountRequest(90m), Json);
        var message = await Client.PostAsJsonAsync($"/api/v1/listings/{secondListing.Id}/conversations", new SendMessageRequest("Salut"), Json);
        var reservation = await Client.PostAsync($"/api/v1/listings/{secondListing.Id}/reservations", content: null);

        // Assert
        offer.StatusCode.Should().Be(HttpStatusCode.Created);
        message.StatusCode.Should().Be(HttpStatusCode.Created);
        reservation.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_ThirdMemberActs_Then_ThirdMemberIsNotAffected()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _third, _listing, SellerBlocksBuyer());
        AuthenticateAs(_third.Id);

        // Act
        var offer = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/offers", new OfferAmountRequest(90m), Json);
        var reservation = await Client.PostAsync($"/api/v1/listings/{_listing.Id}/reservations", content: null);

        // Assert
        offer.StatusCode.Should().Be(HttpStatusCode.Created);
        reservation.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Given_BuyerBlockedThenUnblocked_When_BuyerActsAgain_Then_Returns201()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_seller.Id);
        (await Client.PostAsync($"/api/v1/members/{_buyer.Id}/block", content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuthenticateAs(_buyer.Id);
        await AssertBlockedAsync(await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/offers", new OfferAmountRequest(90m), Json));
        AuthenticateAs(_seller.Id);
        (await Client.DeleteAsync($"/api/v1/members/{_buyer.Id}/block")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/offers", new OfferAmountRequest(90m), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await QueryDbAsync(db => db.Set<Offer>().CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task Given_PendingOffersAndConversationAndReservation_When_SellerBlocksBuyer_Then_OffersRejectedWithoutNotificationAndRestStays()
    {
        // Arrange
        var secondListing = ActiveListing(_seller.Id, "Calculator");
        var buyersListing = ActiveListing(_buyer.Id, "Rucsac");
        var reservedListing = ActiveListing(_seller.Id, "Casca");
        var conversation = Conversation.Start(_buyer.Id, _listing, "Salut", CreatedAt).Value;
        var buyerOffer = BuyerOffer(_listing, _buyer.Id, 80m);
        var secondOffer = BuyerOffer(secondListing, _buyer.Id, 50m, minutes: 2);
        var sellerAsBuyerOffer = BuyerOffer(buyersListing, _seller.Id, 40m, minutes: 3);
        var counteredOffer = BuyerOffer(reservedListing, _buyer.Id, 30m, minutes: 4);
        var counterOnIt = counteredOffer.Counter(_seller.Id, reservedListing, 45m, CreatedAt.AddMinutes(5)).Value;
        var thirdsOffer = BuyerOffer(_listing, _third.Id, 70m, minutes: 6);
        reservedListing.Reserve(_buyer.Id, CreatedAt.AddMinutes(7));
        await SeedAsync(_seller, _buyer, _third, _listing, secondListing, buyersListing, reservedListing, conversation,
            buyerOffer, secondOffer, sellerAsBuyerOffer, counteredOffer, counterOnIt, thirdsOffer);
        AuthenticateAs(_seller.Id);
        var before = DateTimeOffset.UtcNow.ToMicroseconds();

        // Act
        var response = await Client.PostAsync($"/api/v1/members/{_buyer.Id}/block", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var offers = await QueryDbAsync(db => db.Set<Offer>().AsNoTracking().ToDictionaryAsync(o => o.Id));
        foreach (var id in new[] { buyerOffer.Id, secondOffer.Id, sellerAsBuyerOffer.Id, counterOnIt.Id })
        {
            offers[id].Status.Should().Be(OfferStatus.Rejected);
            offers[id].RespondedAt.Should().BeOnOrAfter(before);
        }
        offers[counteredOffer.Id].Status.Should().Be(OfferStatus.Countered);
        offers[thirdsOffer.Id].Status.Should().Be(OfferStatus.Pending);
        (await QueryDbAsync(db => db.Set<Notification>().CountAsync())).Should().Be(0);
        var savedConversation = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().Include(c => c.Messages).SingleAsync());
        savedConversation.Messages.Should().ContainSingle();
        var reserved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == reservedListing.Id));
        reserved.Status.Should().Be(ListingStatus.Reserved);
        reserved.Reservations.Should().ContainSingle().Which.Status.Should().Be(ReservationStatus.Active);

        AuthenticateAs(_buyer.Id);
        var read = await Client.GetFromJsonAsync<GetConversationResponse>($"/api/v1/conversations/{conversation.Id}", Json);
        read!.Offers.Should().ContainSingle().Which.Status.Should().Be(OfferStatus.Rejected);
    }
}

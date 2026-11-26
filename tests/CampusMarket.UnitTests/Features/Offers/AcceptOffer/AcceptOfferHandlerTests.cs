using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Listings.ReserveListing;
using CampusMarket.Api.Features.Offers.AcceptOffer;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Offers.AcceptOffer;

public class AcceptOfferHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(1);

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _buyer = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Member _otherBuyer = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly Member _thirdBuyer = Member.Create("Maria Rus", "maria@campus.ro", Now).Value;
    private readonly Member _stranger = Member.Create("Radu Vlad", "radu@campus.ro", Now).Value;
    private readonly Listing _listing;
    private readonly Offer _offer;
    private readonly FakeListingRepository _listings = new();
    private readonly FakeMemberRepository _members = new();
    private readonly FakeOfferRepository _offers = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly AcceptOfferHandler _handler;

    public AcceptOfferHandlerTests()
    {
        _listing = NewListing("Algebra book");
        foreach (var member in new[] { _seller, _buyer, _otherBuyer, _thirdBuyer, _stranger })
            _members.Items[member.Id] = member;
        _offer = Offer.Make(_listing, _buyer.Id, 90m, Now).Value;
        _offers.Add(_offer);
        _handler = new AcceptOfferHandler(_offers, _listings, _members, _notifications, _unitOfWork, new FixedTimeProvider(Later));
    }

    private Listing NewListing(string title)
    {
        var listing = Listing.Create(_seller.Id, title, Category.Textbook, Condition.Good, 100m, Now).Value;
        listing.Publish(Now.AddDays(30));
        _listings.Items[listing.Id] = listing;
        return listing;
    }

    // An Active reservation of the member on another listing of the same seller.
    private Reservation ReserveOnAnotherListing(Member member, string title) =>
        NewListing(title).Reserve(member.Id, Now).Value;

    private void AssertNothingSaved()
    {
        _offer.Status.Should().Be(OfferStatus.Pending);
        _offer.RespondedAt.Should().BeNull();
        _listing.Reservations.Should().BeEmpty();
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_BuyerOffer_When_SellerAccepts_Then_ReservesListingAtAgreedPriceAndNotifiesBothSides()
    {
        var result = await _handler.HandleAsync(new AcceptOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _offer.Status.Should().Be(OfferStatus.Accepted);
        _offer.RespondedAt.Should().Be(Later);
        _listing.Status.Should().Be(ListingStatus.Reserved);
        _listing.Reservations.Should().ContainSingle();
        var reservation = _listing.Reservations[0];
        reservation.MemberId.Should().Be(_buyer.Id);
        reservation.AgreedPrice.Should().Be(90m);
        reservation.Status.Should().Be(ReservationStatus.Active);
        result.Value.Offer.Id.Should().Be(_offer.Id);
        result.Value.Offer.Status.Should().Be(OfferStatus.Accepted);
        result.Value.Reservation.Id.Should().Be(reservation.Id);
        result.Value.Reservation.ListingId.Should().Be(_listing.Id);
        result.Value.Reservation.BuyerId.Should().Be(_buyer.Id);
        result.Value.Reservation.AgreedPrice.Should().Be(90m);
        result.Value.Reservation.Status.Should().Be(ReservationStatus.Active);
        result.Value.Reservation.CreatedAt.Should().Be(Later);
        _notifications.Items.Should().HaveCount(2);
        _notifications.Items.Should().OnlyContain(n => n.Type == NotificationType.OfferAccepted && n.ListingId == _listing.Id && n.CreatedAt == Later);
        _notifications.Items.Single(n => n.MemberId == _buyer.Id).Message
            .Should().Be("Ion Ionescu accepted your offer of 90 lei for Algebra book");
        _notifications.Items.Single(n => n.MemberId == _seller.Id).Message
            .Should().Be("You accepted the offer of 90 lei for Algebra book");
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SellerCounterOffer_When_BuyerAccepts_Then_SellerNotificationNamesTheBuyer()
    {
        var counter = _offer.Counter(_seller.Id, _listing, 95m, Now.AddMinutes(1)).Value;
        _offers.Add(counter);

        var result = await _handler.HandleAsync(new AcceptOfferCommand(_buyer.Id, counter.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        counter.Status.Should().Be(OfferStatus.Accepted);
        _offer.Status.Should().Be(OfferStatus.Countered);
        _listing.Reservations.Should().ContainSingle().Which.AgreedPrice.Should().Be(95m);
        _listing.Reservations[0].MemberId.Should().Be(_buyer.Id);
        _notifications.Items.Should().HaveCount(2);
        _notifications.Items.Single(n => n.MemberId == _seller.Id).Message
            .Should().Be("Ana Pop accepted your offer of 95 lei for Algebra book");
        _notifications.Items.Single(n => n.MemberId == _buyer.Id).Message
            .Should().Be("You accepted the offer of 95 lei for Algebra book");
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_PendingOffersOfOtherBuyers_When_SellerAccepts_Then_TheyAreRejectedAndTheirBuyersNotified()
    {
        var second = Offer.Make(_listing, _otherBuyer.Id, 80m, Now.AddMinutes(1)).Value;
        var third = Offer.Make(_listing, _thirdBuyer.Id, 70.5m, Now.AddMinutes(2)).Value;
        _offers.Add(second);
        _offers.Add(third);

        var result = await _handler.HandleAsync(new AcceptOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        second.Status.Should().Be(OfferStatus.Rejected);
        second.RespondedAt.Should().Be(Later);
        third.Status.Should().Be(OfferStatus.Rejected);
        third.RespondedAt.Should().Be(Later);
        _offer.Status.Should().Be(OfferStatus.Accepted);
        _notifications.Items.Should().HaveCount(4);
        var closedForSecond = _notifications.Items.Single(n => n.MemberId == _otherBuyer.Id);
        closedForSecond.Type.Should().Be(NotificationType.OfferRejected);
        closedForSecond.Message.Should().Be("The offer of 80 lei for Algebra book was closed: the listing was reserved by another buyer");
        closedForSecond.ListingId.Should().Be(_listing.Id);
        var closedForThird = _notifications.Items.Single(n => n.MemberId == _thirdBuyer.Id);
        closedForThird.Type.Should().Be(NotificationType.OfferRejected);
        closedForThird.Message.Should().Be("The offer of 70.5 lei for Algebra book was closed: the listing was reserved by another buyer");
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_AcceptedOfferStillPendingInStore_When_SellerAccepts_Then_AcceptedBuyerGetsNoRejectedNotification()
    {
        var other = Offer.Make(_listing, _otherBuyer.Id, 80m, Now.AddMinutes(1)).Value;
        _offers.Add(other);

        var result = await _handler.HandleAsync(new AcceptOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _offer.Status.Should().Be(OfferStatus.Accepted);
        other.Status.Should().Be(OfferStatus.Rejected);
        _notifications.Items.Where(n => n.Type == NotificationType.OfferRejected)
            .Should().ContainSingle().Which.MemberId.Should().Be(_otherBuyer.Id);
        _notifications.Items.Where(n => n.MemberId == _buyer.Id)
            .Should().ContainSingle().Which.Type.Should().Be(NotificationType.OfferAccepted);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_AnsweredOffersOfOtherBuyers_When_SellerAccepts_Then_TheyAreUntouchedWithoutNotification()
    {
        var rejected = Offer.Make(_listing, _otherBuyer.Id, 80m, Now.AddMinutes(1)).Value;
        rejected.Reject(_seller.Id, Now.AddMinutes(2));
        var withdrawn = Offer.Make(_listing, _thirdBuyer.Id, 70m, Now.AddMinutes(1)).Value;
        withdrawn.Withdraw(_thirdBuyer.Id, Now.AddMinutes(3));
        _offers.Add(rejected);
        _offers.Add(withdrawn);

        var result = await _handler.HandleAsync(new AcceptOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        rejected.Status.Should().Be(OfferStatus.Rejected);
        rejected.RespondedAt.Should().Be(Now.AddMinutes(2));
        withdrawn.Status.Should().Be(OfferStatus.Withdrawn);
        withdrawn.RespondedAt.Should().Be(Now.AddMinutes(3));
        _notifications.Items.Should().HaveCount(2);
        _notifications.Items.Should().NotContain(n => n.MemberId == _otherBuyer.Id || n.MemberId == _thirdBuyer.Id);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_BuyerWithTwoActiveReservations_When_SellerAccepts_Then_Succeeds()
    {
        ReserveOnAnotherListing(_buyer, "Calculus");
        ReserveOnAnotherListing(_buyer, "Physics");

        var result = await _handler.HandleAsync(new AcceptOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _listing.Status.Should().Be(ListingStatus.Reserved);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_BuyerWithThreeActiveReservations_When_SellerAccepts_Then_ReturnsBuyerReservationLimitAndSavesNothing()
    {
        ReserveOnAnotherListing(_buyer, "Calculus");
        ReserveOnAnotherListing(_buyer, "Physics");
        ReserveOnAnotherListing(_buyer, "Chemistry");
        ReserveListingHandler.MaxActiveReservations.Should().Be(3);

        var result = await _handler.HandleAsync(new AcceptOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.BuyerReservationLimit");
        result.Error.Message.Should().Be("The buyer already holds 3 active reservations.");
        AssertNothingSaved();
        _listing.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public async Task Given_BuyerWithThreeReservationsButTwoNotActive_When_SellerAccepts_Then_Succeeds()
    {
        ReserveOnAnotherListing(_buyer, "Calculus");
        var cancelledListing = NewListing("Physics");
        var cancelled = cancelledListing.Reserve(_buyer.Id, Now).Value;
        cancelledListing.CancelReservation(cancelled.Id);
        var expiredListing = NewListing("Chemistry");
        var expired = expiredListing.Reserve(_buyer.Id, Now).Value;
        expiredListing.ExpireReservations(Now.AddDays(3));
        cancelled.Status.Should().Be(ReservationStatus.Cancelled);
        expired.Status.Should().Be(ReservationStatus.Expired);

        var result = await _handler.HandleAsync(new AcceptOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _listing.Status.Should().Be(ListingStatus.Reserved);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ReservationsOfOtherMembers_When_SellerAccepts_Then_OnlyTheBuyersReservationsCount()
    {
        ReserveOnAnotherListing(_otherBuyer, "Calculus");
        ReserveOnAnotherListing(_otherBuyer, "Physics");
        ReserveOnAnotherListing(_otherBuyer, "Chemistry");
        ReserveOnAnotherListing(_buyer, "Biology");

        var result = await _handler.HandleAsync(new AcceptOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownOffer_When_Accepting_Then_ReturnsOfferNotFoundAndSavesNothing()
    {
        var offerId = Guid.NewGuid();

        var result = await _handler.HandleAsync(new AcceptOfferCommand(_seller.Id, offerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Offer.NotFound");
        result.Error.Message.Should().Be($"Offer '{offerId}' was not found.");
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_Proposer_When_Accepting_Then_ReturnsNotRecipientAndSavesNothing()
    {
        var result = await _handler.HandleAsync(new AcceptOfferCommand(_buyer.Id, _offer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Offer.NotRecipient");
        result.Error.Message.Should().Be("Only the other side can answer this offer.");
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_Stranger_When_Accepting_Then_ReturnsNotParticipantAndSavesNothing()
    {
        var result = await _handler.HandleAsync(new AcceptOfferCommand(_stranger.Id, _offer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Offer.NotParticipant");
        result.Error.Message.Should().Be("Only the buyer and the seller can act on this offer.");
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_AnsweredOffer_When_Accepting_Then_ReturnsNotPendingAndSavesNothing()
    {
        _offer.Reject(_seller.Id, Now.AddMinutes(1));

        var result = await _handler.HandleAsync(new AcceptOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.NotPending");
        result.Error.Message.Should().Be("The offer was already answered.");
        _offer.Status.Should().Be(OfferStatus.Rejected);
        _offer.RespondedAt.Should().Be(Now.AddMinutes(1));
        _listing.Reservations.Should().BeEmpty();
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingReservedMeanwhile_When_SellerAccepts_Then_ReturnsListingNotActiveAndKeepsOtherOffersPending()
    {
        var other = Offer.Make(_listing, _otherBuyer.Id, 80m, Now.AddMinutes(1)).Value;
        _offers.Add(other);
        _listing.Reserve(_stranger.Id, Now.AddMinutes(2));

        var result = await _handler.HandleAsync(new AcceptOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.ListingNotActive");
        result.Error.Message.Should().Be("Offers can only be made and accepted on an active listing.");
        _offer.Status.Should().Be(OfferStatus.Pending);
        _offer.RespondedAt.Should().BeNull();
        other.Status.Should().Be(OfferStatus.Pending);
        _listing.Reservations.Should().ContainSingle().Which.MemberId.Should().Be(_stranger.Id);
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }
}

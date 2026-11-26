using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Offers.CounterOffer;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Offers.CounterOffer;

public class CounterOfferHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(1);

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _buyer = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Member _stranger = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly Listing _listing;
    private readonly Offer _offer;
    private readonly FakeListingRepository _listings = new();
    private readonly FakeMemberRepository _members = new();
    private readonly FakeOfferRepository _offers = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CounterOfferHandler _handler;

    public CounterOfferHandlerTests()
    {
        _listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 100m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        _listings.Items[_listing.Id] = _listing;
        _members.Items[_seller.Id] = _seller;
        _members.Items[_buyer.Id] = _buyer;
        _members.Items[_stranger.Id] = _stranger;
        _offer = Offer.Make(_listing, _buyer.Id, 90m, Now).Value;
        _offers.Add(_offer);
        _handler = new CounterOfferHandler(_offers, _listings, _members, _notifications, new FakeMemberBlockRepository(), _unitOfWork, new FixedTimeProvider(Later));
    }

    private void AssertNothingChanged()
    {
        _offers.Items.Should().ContainSingle().Which.Should().BeSameAs(_offer);
        _offer.Status.Should().Be(OfferStatus.Pending);
        _offer.RespondedAt.Should().BeNull();
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_BuyerOffer_When_SellerCounters_Then_AddsCounterAndNotifiesBuyer()
    {
        var result = await _handler.HandleAsync(new CounterOfferCommand(_seller.Id, _offer.Id, 95m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _offer.Status.Should().Be(OfferStatus.Countered);
        _offer.RespondedAt.Should().Be(Later);
        _offers.Items.Should().HaveCount(2);
        var counter = _offers.Items[1];
        counter.Status.Should().Be(OfferStatus.Pending);
        counter.Amount.Should().Be(95m);
        counter.ProposedById.Should().Be(_seller.Id);
        counter.ParentOfferId.Should().Be(_offer.Id);
        counter.CreatedAt.Should().Be(Later);
        result.Value.Id.Should().Be(counter.Id);
        result.Value.ParentOfferId.Should().Be(_offer.Id);
        result.Value.ProposedById.Should().Be(_seller.Id);
        result.Value.Amount.Should().Be(95m);
        result.Value.Status.Should().Be(OfferStatus.Pending);
        _notifications.Items.Should().ContainSingle();
        var notification = _notifications.Items[0];
        notification.Type.Should().Be(NotificationType.OfferCountered);
        notification.MemberId.Should().Be(_buyer.Id);
        notification.Message.Should().Be("Ion Ionescu countered with 95 lei for Algebra book");
        notification.ListingId.Should().Be(_listing.Id);
        notification.CreatedAt.Should().Be(Later);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SellerCounterOffer_When_BuyerCounters_Then_NotifiesSellerWithBuyerName()
    {
        var sellerCounter = _offer.Counter(_seller.Id, _listing, 95m, Now.AddMinutes(1)).Value;
        _offers.Add(sellerCounter);

        var result = await _handler.HandleAsync(new CounterOfferCommand(_buyer.Id, sellerCounter.Id, 92.5m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sellerCounter.Status.Should().Be(OfferStatus.Countered);
        _offers.Items.Should().HaveCount(3);
        _offers.Items[2].ParentOfferId.Should().Be(sellerCounter.Id);
        _offers.Items[2].ProposedById.Should().Be(_buyer.Id);
        _notifications.Items.Should().ContainSingle();
        _notifications.Items[0].Type.Should().Be(NotificationType.OfferCountered);
        _notifications.Items[0].MemberId.Should().Be(_seller.Id);
        _notifications.Items[0].Message.Should().Be("Ana Pop countered with 92.5 lei for Algebra book");
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownOffer_When_Countering_Then_ReturnsOfferNotFoundAndSavesNothing()
    {
        var offerId = Guid.NewGuid();

        var result = await _handler.HandleAsync(new CounterOfferCommand(_seller.Id, offerId, 95m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Offer.NotFound");
        result.Error.Message.Should().Be($"Offer '{offerId}' was not found.");
        AssertNothingChanged();
    }

    [Fact]
    public async Task Given_Proposer_When_Countering_Then_ReturnsNotRecipientAndSavesNothing()
    {
        var result = await _handler.HandleAsync(new CounterOfferCommand(_buyer.Id, _offer.Id, 80m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Offer.NotRecipient");
        result.Error.Message.Should().Be("Only the other side can answer this offer.");
        AssertNothingChanged();
    }

    [Fact]
    public async Task Given_Stranger_When_Countering_Then_ReturnsNotParticipantAndSavesNothing()
    {
        var result = await _handler.HandleAsync(new CounterOfferCommand(_stranger.Id, _offer.Id, 80m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Offer.NotParticipant");
        result.Error.Message.Should().Be("Only the buyer and the seller can act on this offer.");
        AssertNothingChanged();
    }

    [Fact]
    public async Task Given_AnsweredOffer_When_Countering_Then_ReturnsNotPendingAndSavesNothing()
    {
        _offer.Reject(_seller.Id, Now.AddMinutes(1));
        var respondedAt = _offer.RespondedAt;

        var result = await _handler.HandleAsync(new CounterOfferCommand(_seller.Id, _offer.Id, 95m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.NotPending");
        result.Error.Message.Should().Be("The offer was already answered.");
        _offers.Items.Should().ContainSingle();
        _offer.Status.Should().Be(OfferStatus.Rejected);
        _offer.RespondedAt.Should().Be(respondedAt);
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ReservedListing_When_Countering_Then_ReturnsListingNotActiveAndSavesNothing()
    {
        _listing.Reserve(_stranger.Id, Now);

        var result = await _handler.HandleAsync(new CounterOfferCommand(_seller.Id, _offer.Id, 95m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.ListingNotActive");
        result.Error.Message.Should().Be("Offers can only be made and accepted on an active listing.");
        AssertNothingChanged();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Given_AmountNotPositive_When_Countering_Then_ReturnsAmountInvalidAndSavesNothing(int amount)
    {
        var result = await _handler.HandleAsync(new CounterOfferCommand(_seller.Id, _offer.Id, amount), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.AmountInvalid");
        result.Error.Message.Should().Be("The offered amount must be greater than 0.");
        AssertNothingChanged();
    }

    [Fact]
    public async Task Given_AmountAboveMaximum_When_Countering_Then_ReturnsAmountTooHighAndSavesNothing()
    {
        var result = await _handler.HandleAsync(
            new CounterOfferCommand(_seller.Id, _offer.Id, Money.MaxAmount + 0.01m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.AmountTooHigh");
        result.Error.Message.Should().Be($"The offered amount must be at most {Money.MaxAmount} lei.");
        AssertNothingChanged();
    }
}

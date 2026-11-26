using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Offers.RejectOffer;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Offers.RejectOffer;

public class RejectOfferHandlerTests
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
    private readonly RejectOfferHandler _handler;

    public RejectOfferHandlerTests()
    {
        _listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 100m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        _listings.Items[_listing.Id] = _listing;
        _members.Items[_seller.Id] = _seller;
        _members.Items[_buyer.Id] = _buyer;
        _members.Items[_stranger.Id] = _stranger;
        _offer = Offer.Make(_listing, _buyer.Id, 90m, Now).Value;
        _offers.Add(_offer);
        _handler = new RejectOfferHandler(_offers, _listings, _members, _notifications, _unitOfWork, new FixedTimeProvider(Later));
    }

    private void AssertNothingChanged(OfferStatus status = OfferStatus.Pending)
    {
        _offers.Items.Should().ContainSingle().Which.Should().BeSameAs(_offer);
        _offer.Status.Should().Be(status);
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_BuyerOffer_When_SellerRejects_Then_OfferRejectedAndBuyerNotified()
    {
        var result = await _handler.HandleAsync(new RejectOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _offer.Status.Should().Be(OfferStatus.Rejected);
        _offer.RespondedAt.Should().Be(Later);
        result.Value.Id.Should().Be(_offer.Id);
        result.Value.Status.Should().Be(OfferStatus.Rejected);
        result.Value.RespondedAt.Should().Be(Later);
        _offers.Items.Should().ContainSingle();
        _listing.Status.Should().Be(ListingStatus.Active);
        _notifications.Items.Should().ContainSingle();
        var notification = _notifications.Items[0];
        notification.Type.Should().Be(NotificationType.OfferRejected);
        notification.MemberId.Should().Be(_buyer.Id);
        notification.Message.Should().Be("Ion Ionescu rejected your offer of 90 lei for Algebra book");
        notification.ListingId.Should().Be(_listing.Id);
        notification.CreatedAt.Should().Be(Later);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SellerCounterOffer_When_BuyerRejects_Then_NotifiesSellerWithBuyerName()
    {
        var counter = _offer.Counter(_seller.Id, _listing, 95.5m, Now.AddMinutes(1)).Value;
        _offers.Add(counter);

        var result = await _handler.HandleAsync(new RejectOfferCommand(_buyer.Id, counter.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        counter.Status.Should().Be(OfferStatus.Rejected);
        _notifications.Items.Should().ContainSingle();
        _notifications.Items[0].Type.Should().Be(NotificationType.OfferRejected);
        _notifications.Items[0].MemberId.Should().Be(_seller.Id);
        _notifications.Items[0].Message.Should().Be("Ana Pop rejected your offer of 95.5 lei for Algebra book");
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownOffer_When_Rejecting_Then_ReturnsOfferNotFoundAndSavesNothing()
    {
        var offerId = Guid.NewGuid();

        var result = await _handler.HandleAsync(new RejectOfferCommand(_seller.Id, offerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Offer.NotFound");
        result.Error.Message.Should().Be($"Offer '{offerId}' was not found.");
        AssertNothingChanged();
    }

    [Fact]
    public async Task Given_Proposer_When_Rejecting_Then_ReturnsNotRecipientAndSavesNothing()
    {
        var result = await _handler.HandleAsync(new RejectOfferCommand(_buyer.Id, _offer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Offer.NotRecipient");
        result.Error.Message.Should().Be("Only the other side can answer this offer.");
        AssertNothingChanged();
        _offer.RespondedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_Stranger_When_Rejecting_Then_ReturnsNotParticipantAndSavesNothing()
    {
        var result = await _handler.HandleAsync(new RejectOfferCommand(_stranger.Id, _offer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Offer.NotParticipant");
        result.Error.Message.Should().Be("Only the buyer and the seller can act on this offer.");
        AssertNothingChanged();
        _offer.RespondedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_AnsweredOffer_When_Rejecting_Then_ReturnsNotPendingAndSavesNothing()
    {
        _offer.Withdraw(_buyer.Id, Now.AddMinutes(1));

        var result = await _handler.HandleAsync(new RejectOfferCommand(_seller.Id, _offer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.NotPending");
        result.Error.Message.Should().Be("The offer was already answered.");
        AssertNothingChanged(OfferStatus.Withdrawn);
        _offer.RespondedAt.Should().Be(Now.AddMinutes(1));
    }
}

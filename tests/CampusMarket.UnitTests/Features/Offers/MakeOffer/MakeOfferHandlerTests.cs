using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Offers.MakeOffer;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Offers.MakeOffer;

public class MakeOfferHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _buyer = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Member _otherBuyer = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly Listing _listing;
    private readonly FakeListingRepository _listings = new();
    private readonly FakeMemberRepository _members = new();
    private readonly FakeOfferRepository _offers = new();
    private readonly FakeConversationRepository _conversations = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MakeOfferHandler _handler;

    public MakeOfferHandlerTests()
    {
        _listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 150m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        _listings.Items[_listing.Id] = _listing;
        _members.Items[_seller.Id] = _seller;
        _members.Items[_buyer.Id] = _buyer;
        _members.Items[_otherBuyer.Id] = _otherBuyer;
        _handler = new MakeOfferHandler(_listings, _members, _offers, _conversations, _notifications, new FakeMemberBlockRepository(), _unitOfWork, new FixedTimeProvider(Now));
    }

    private void AssertNothingSaved()
    {
        _offers.Items.Should().BeEmpty();
        _conversations.Items.Should().BeEmpty();
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AmountWithTwoDecimalDigits_When_BuyerMakesOffer_Then_FirstMessageHasNoTrailingZero()
    {
        var command = new MakeOfferCommand(_buyer.Id, _listing.Id, 99.50m);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _conversations.Items.Should().ContainSingle()
            .Which.Messages.Should().ContainSingle()
            .Which.Text.Should().Be("Am făcut o ofertă de 99.5 lei.");
    }

    [Theory]
    [InlineData(120, "Am făcut o ofertă de 120 lei.", "Ana Pop offered 120 lei for Algebra book")]
    [InlineData(99.5, "Am făcut o ofertă de 99.5 lei.", "Ana Pop offered 99.5 lei for Algebra book")]
    public async Task Given_NoConversation_When_BuyerMakesOffer_Then_StartsConversationAddsOfferAndNotifiesSeller(
        double amount, string firstMessage, string notificationText)
    {
        var command = new MakeOfferCommand(_buyer.Id, _listing.Id, (decimal)amount);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _conversations.Items.Should().ContainSingle();
        var conversation = _conversations.Items[0];
        conversation.ListingId.Should().Be(_listing.Id);
        conversation.BuyerId.Should().Be(_buyer.Id);
        conversation.SellerId.Should().Be(_seller.Id);
        conversation.Messages.Should().ContainSingle();
        conversation.Messages[0].SenderId.Should().Be(_buyer.Id);
        conversation.Messages[0].Text.Should().Be(firstMessage);
        _offers.Items.Should().ContainSingle();
        var offer = _offers.Items[0];
        offer.ListingId.Should().Be(_listing.Id);
        offer.BuyerId.Should().Be(_buyer.Id);
        offer.SellerId.Should().Be(_seller.Id);
        offer.ProposedById.Should().Be(_buyer.Id);
        offer.Amount.Should().Be((decimal)amount);
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.CreatedAt.Should().Be(Now);
        _notifications.Items.Should().ContainSingle();
        var notification = _notifications.Items[0];
        notification.Type.Should().Be(NotificationType.OfferReceived);
        notification.MemberId.Should().Be(_seller.Id);
        notification.Message.Should().Be(notificationText);
        notification.ListingId.Should().Be(_listing.Id);
        notification.CreatedAt.Should().Be(Now);
        notification.IsRead.Should().BeFalse();
        _unitOfWork.SaveCount.Should().Be(1);
        result.Value.ConversationId.Should().Be(conversation.Id);
        result.Value.Offer.Id.Should().Be(offer.Id);
        result.Value.Offer.Amount.Should().Be((decimal)amount);
        result.Value.Offer.Status.Should().Be(OfferStatus.Pending);
        result.Value.Offer.ProposedById.Should().Be(_buyer.Id);
    }

    [Fact]
    public async Task Given_ExistingConversation_When_BuyerMakesOffer_Then_ReusesItWithoutNewMessage()
    {
        var existing = Conversation.Start(_buyer.Id, _listing, "Mai e disponibil?", Now.AddMinutes(-5)).Value;
        await _conversations.AddAsync(existing);
        var command = new MakeOfferCommand(_buyer.Id, _listing.Id, 120m);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ConversationId.Should().Be(existing.Id);
        _conversations.Items.Should().ContainSingle().Which.Should().BeSameAs(existing);
        existing.Messages.Should().ContainSingle();
        existing.Messages[0].Text.Should().Be("Mai e disponibil?");
        _offers.Items.Should().ContainSingle();
        _notifications.Items.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownListing_When_MakingOffer_Then_ReturnsListingNotFoundAndSavesNothing()
    {
        var listingId = Guid.NewGuid();

        var result = await _handler.HandleAsync(new MakeOfferCommand(_buyer.Id, listingId, 120m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' was not found.");
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_UnknownListingAndUnknownMember_When_MakingOffer_Then_ReturnsListingNotFound()
    {
        var listingId = Guid.NewGuid();

        var result = await _handler.HandleAsync(new MakeOfferCommand(Guid.NewGuid(), listingId, 120m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Listing.NotFound");
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_UnknownMember_When_MakingOffer_Then_ReturnsMemberNotFoundAndSavesNothing()
    {
        var memberId = Guid.NewGuid();

        var result = await _handler.HandleAsync(new MakeOfferCommand(memberId, _listing.Id, 120m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{memberId}' was not found.");
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_SellerAsBuyer_When_MakingOffer_Then_ReturnsOwnListingAndSavesNothing()
    {
        var result = await _handler.HandleAsync(new MakeOfferCommand(_seller.Id, _listing.Id, 120m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.OwnListing");
        result.Error.Message.Should().Be("A seller cannot make an offer on their own listing.");
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_ReservedListing_When_MakingOffer_Then_ReturnsListingNotActiveAndSavesNothing()
    {
        _listing.Reserve(_otherBuyer.Id, Now);

        var result = await _handler.HandleAsync(new MakeOfferCommand(_buyer.Id, _listing.Id, 120m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.ListingNotActive");
        result.Error.Message.Should().Be("Offers can only be made and accepted on an active listing.");
        AssertNothingSaved();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Given_AmountNotPositive_When_MakingOffer_Then_ReturnsAmountInvalidAndSavesNothing(int amount)
    {
        var result = await _handler.HandleAsync(new MakeOfferCommand(_buyer.Id, _listing.Id, amount), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.AmountInvalid");
        result.Error.Message.Should().Be("The offered amount must be greater than 0.");
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_AmountAboveMaximum_When_MakingOffer_Then_ReturnsAmountTooHighAndSavesNothing()
    {
        var result = await _handler.HandleAsync(
            new MakeOfferCommand(_buyer.Id, _listing.Id, Money.MaxAmount + 0.01m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.AmountTooHigh");
        result.Error.Message.Should().Be($"The offered amount must be at most {Money.MaxAmount} lei.");
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_PendingOfferOfThisBuyer_When_MakingAnotherOffer_Then_ReturnsAlreadyPendingAndAddsNothing()
    {
        var pending = Offer.Make(_listing, _buyer.Id, 100m, Now.AddMinutes(-5)).Value;
        _offers.Add(pending);

        var result = await _handler.HandleAsync(new MakeOfferCommand(_buyer.Id, _listing.Id, 120m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Offer.AlreadyPending");
        result.Error.Message.Should().Be("There is already a pending offer between you and the seller on this listing.");
        _offers.Items.Should().ContainSingle().Which.Should().BeSameAs(pending);
        _conversations.Items.Should().BeEmpty();
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_PendingCounterOfferFromSeller_When_BuyerMakesOffer_Then_ReturnsAlreadyPending()
    {
        var first = Offer.Make(_listing, _buyer.Id, 100m, Now.AddMinutes(-5)).Value;
        var counter = first.Counter(_seller.Id, _listing, 140m, Now.AddMinutes(-4)).Value;
        _offers.Add(first);
        _offers.Add(counter);

        var result = await _handler.HandleAsync(new MakeOfferCommand(_buyer.Id, _listing.Id, 120m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Offer.AlreadyPending");
        _offers.Items.Should().HaveCount(2);
        _conversations.Items.Should().BeEmpty();
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_InvalidAmountAndPendingOffer_When_MakingOffer_Then_ReturnsDomainErrorBeforeAlreadyPending()
    {
        _offers.Add(Offer.Make(_listing, _buyer.Id, 100m, Now.AddMinutes(-5)).Value);

        var result = await _handler.HandleAsync(new MakeOfferCommand(_buyer.Id, _listing.Id, 0m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.AmountInvalid");
        _offers.Items.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_PendingOfferOfAnotherBuyer_When_BuyerMakesOffer_Then_Succeeds()
    {
        _offers.Add(Offer.Make(_listing, _otherBuyer.Id, 100m, Now.AddMinutes(-5)).Value);

        var result = await _handler.HandleAsync(new MakeOfferCommand(_buyer.Id, _listing.Id, 120m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _offers.Items.Should().HaveCount(2);
        _offers.Items.Count(o => o.BuyerId == _buyer.Id && o.Status == OfferStatus.Pending).Should().Be(1);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Rejected)]
    [InlineData(OfferStatus.Countered)]
    [InlineData(OfferStatus.Withdrawn)]
    public async Task Given_NonPendingOfferOfThisBuyer_When_BuyerMakesOffer_Then_Succeeds(OfferStatus status)
    {
        var old = Offer.Make(_listing, _buyer.Id, 100m, Now.AddMinutes(-5)).Value;
        switch (status)
        {
            case OfferStatus.Rejected:
                old.Reject(_seller.Id, Now.AddMinutes(-4));
                break;
            case OfferStatus.Countered:
                old.Counter(_seller.Id, _listing, 140m, Now.AddMinutes(-4));
                break;
            case OfferStatus.Withdrawn:
                old.Withdraw(_buyer.Id, Now.AddMinutes(-4));
                break;
            case OfferStatus.Accepted:
                old.Accept(_seller.Id, _listing, Now.AddMinutes(-4));
                _listing.CancelReservation(_listing.Reservations[0].Id);
                break;
        }

        old.Status.Should().Be(status);
        _offers.Add(old);

        var result = await _handler.HandleAsync(new MakeOfferCommand(_buyer.Id, _listing.Id, 120m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _offers.Items.Should().HaveCount(2);
        _offers.Items.Count(o => o.Status == OfferStatus.Pending).Should().Be(1);
        _unitOfWork.SaveCount.Should().Be(1);
    }
}

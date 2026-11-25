using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Offers.WithdrawOffer;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Offers.WithdrawOffer;

public class WithdrawOfferHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(1);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrangerId = new("44444444-4444-4444-4444-444444444444");

    private readonly Listing _listing;
    private readonly Offer _offer;
    private readonly FakeOfferRepository _offers = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly WithdrawOfferHandler _handler;

    public WithdrawOfferHandlerTests()
    {
        _listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 100m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        _offer = Offer.Make(_listing, BuyerId, 90m, Now).Value;
        _offers.Add(_offer);
        _handler = new WithdrawOfferHandler(_offers, _unitOfWork, new FixedTimeProvider(Later));
    }

    private void AssertNothingChanged(OfferStatus status = OfferStatus.Pending)
    {
        _offers.Items.Should().ContainSingle().Which.Should().BeSameAs(_offer);
        _offer.Status.Should().Be(status);
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_PendingOffer_When_BuyerWithdraws_Then_OfferWithdrawnAndSavesOnce()
    {
        var result = await _handler.HandleAsync(new WithdrawOfferCommand(BuyerId, _offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _offer.Status.Should().Be(OfferStatus.Withdrawn);
        _offer.RespondedAt.Should().Be(Later);
        result.Value.Id.Should().Be(_offer.Id);
        result.Value.Status.Should().Be(OfferStatus.Withdrawn);
        result.Value.RespondedAt.Should().Be(Later);
        _offers.Items.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownOffer_When_Withdrawing_Then_ReturnsOfferNotFoundAndSavesNothing()
    {
        var offerId = Guid.NewGuid();

        var result = await _handler.HandleAsync(new WithdrawOfferCommand(BuyerId, offerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Offer.NotFound");
        result.Error.Message.Should().Be($"Offer '{offerId}' was not found.");
        AssertNothingChanged();
    }

    [Fact]
    public async Task Given_Seller_When_Withdrawing_Then_ReturnsNotBuyerAndSavesNothing()
    {
        var result = await _handler.HandleAsync(new WithdrawOfferCommand(SellerId, _offer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Offer.NotBuyer");
        result.Error.Message.Should().Be("Only the buyer can withdraw an offer.");
        AssertNothingChanged();
        _offer.RespondedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_Stranger_When_Withdrawing_Then_ReturnsNotParticipantAndSavesNothing()
    {
        var result = await _handler.HandleAsync(new WithdrawOfferCommand(StrangerId, _offer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Offer.NotParticipant");
        result.Error.Message.Should().Be("Only the buyer and the seller can act on this offer.");
        AssertNothingChanged();
        _offer.RespondedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_SellerCounterOffer_When_BuyerWithdraws_Then_ReturnsNotOwnOfferAndSavesNothing()
    {
        var counter = _offer.Counter(SellerId, _listing, 95m, Now.AddMinutes(1)).Value;
        _offers.Items.Clear();
        _offers.Add(counter);

        var result = await _handler.HandleAsync(new WithdrawOfferCommand(BuyerId, counter.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.NotOwnOffer");
        result.Error.Message.Should().Be("Only your own offer can be withdrawn; reject the seller's counter-offer instead.");
        _offers.Items.Should().ContainSingle().Which.Should().BeSameAs(counter);
        counter.Status.Should().Be(OfferStatus.Pending);
        counter.RespondedAt.Should().BeNull();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AnsweredOffer_When_BuyerWithdraws_Then_ReturnsNotPendingAndSavesNothing()
    {
        _offer.Reject(SellerId, Now.AddMinutes(1));

        var result = await _handler.HandleAsync(new WithdrawOfferCommand(BuyerId, _offer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Offer.NotPending");
        result.Error.Message.Should().Be("The offer was already answered.");
        AssertNothingChanged(OfferStatus.Rejected);
        _offer.RespondedAt.Should().Be(Now.AddMinutes(1));
    }
}

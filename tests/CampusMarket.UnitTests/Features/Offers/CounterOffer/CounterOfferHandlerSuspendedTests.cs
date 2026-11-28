using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Offers.CounterOffer;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Offers.CounterOffer;

public class CounterOfferHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _buyer = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Listing _listing;
    private readonly Offer _counter;
    private readonly FakeOfferRepository _offers = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly CounterOfferHandler _handler;

    public CounterOfferHandlerSuspendedTests()
    {
        _listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 150m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        var offer = Offer.Make(_listing, _buyer.Id, 100m, Now).Value;
        _counter = offer.Counter(_seller.Id, _listing, 130m, Now).Value;
        _offers.Add(offer);
        _offers.Add(_counter);
        var listings = new FakeListingRepository();
        listings.Items[_listing.Id] = _listing;
        var members = new FakeMemberRepository();
        members.Items[_seller.Id] = _seller;
        members.Items[_buyer.Id] = _buyer;
        _handler = new CounterOfferHandler(_offers, listings, members, _notifications, new FakeMemberBlockRepository(), _unitOfWork, _clock);
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_BuyerCountersTheSellersOffer_Then_ReturnsListingNotFoundAndAddsNothing()
    {
        _seller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);

        var result = await _handler.HandleAsync(new CounterOfferCommand(_buyer.Id, _counter.Id, 115m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{_listing.Id}' was not found.");
        _counter.Status.Should().Be(OfferStatus.Pending);
        _offers.Items.Should().HaveCount(2);
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SuspensionOver_When_BuyerCountersTheSellersOffer_Then_Succeeds()
    {
        _seller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);
        _clock.Now = Now.AddDays(1);

        var result = await _handler.HandleAsync(new CounterOfferCommand(_buyer.Id, _counter.Id, 115m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _counter.Status.Should().Be(OfferStatus.Countered);
        _offers.Items.Should().HaveCount(3);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SuspendedBuyer_When_BuyerCountersTheSellersOffer_Then_OnlyTheSellerIsChecked()
    {
        _buyer.Suspend(AdminId, "Scam", null, Now);

        var result = await _handler.HandleAsync(new CounterOfferCommand(_buyer.Id, _counter.Id, 115m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _offers.Items.Should().HaveCount(3);
    }
}

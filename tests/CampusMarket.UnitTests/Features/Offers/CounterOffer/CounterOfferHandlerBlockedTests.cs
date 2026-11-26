using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Offers.CounterOffer;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Offers.CounterOffer;

public class CounterOfferHandlerBlockedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _buyer = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Member _stranger = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly Offer _offer;
    private readonly FakeOfferRepository _offers = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CounterOfferHandler _handler;

    public CounterOfferHandlerBlockedTests()
    {
        var listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 100m, Now).Value;
        listing.Publish(Now.AddDays(30));
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        foreach (var member in new[] { _seller, _buyer, _stranger })
            members.Items[member.Id] = member;
        _offer = Offer.Make(listing, _buyer.Id, 90m, Now).Value;
        _offers.Add(_offer);
        _handler = new CounterOfferHandler(_offers, listings, members, _notifications, _blocks, _unitOfWork, new FixedTimeProvider(Now.AddHours(1)));
    }

    private void Block(Member blocker, Member blocked) =>
        _blocks.Add(MemberBlock.Create(blocker.Id, blocked.Id, Now).Value);

    [Fact]
    public async Task Given_ProposerBlockedTheAnsweringMember_When_Countering_Then_FailsWithMemberBlockedAndChangesNothing()
    {
        Block(_buyer, _seller);

        var result = await _handler.HandleAsync(new CounterOfferCommand(_seller.Id, _offer.Id, 95m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Member.Blocked");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        _offers.Items.Should().ContainSingle().Which.Should().BeSameAs(_offer);
        _offer.Status.Should().Be(OfferStatus.Pending);
        _offer.RespondedAt.Should().BeNull();
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AnsweringMemberBlockedTheProposer_When_Countering_Then_Succeeds()
    {
        Block(_seller, _buyer);

        var result = await _handler.HandleAsync(new CounterOfferCommand(_seller.Id, _offer.Id, 95m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _offer.Status.Should().Be(OfferStatus.Countered);
        _offers.Items.Should().HaveCount(2);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_StrangerBlockedByTheProposer_When_Countering_Then_FailsWithNotParticipantNotBlocked()
    {
        Block(_buyer, _stranger);

        var result = await _handler.HandleAsync(new CounterOfferCommand(_stranger.Id, _offer.Id, 95m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.NotParticipant");
        _offer.Status.Should().Be(OfferStatus.Pending);
        _unitOfWork.SaveCount.Should().Be(0);
    }
}

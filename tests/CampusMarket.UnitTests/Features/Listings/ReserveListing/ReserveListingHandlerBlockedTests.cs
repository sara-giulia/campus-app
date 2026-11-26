using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings.ReserveListing;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.ReserveListing;

public class ReserveListingHandlerBlockedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _buyer = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Member _otherBuyer = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly Listing _listing;
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ReserveListingHandler _handler;

    public ReserveListingHandlerBlockedTests()
    {
        _listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        var listings = new FakeListingRepository();
        listings.Items[_listing.Id] = _listing;
        var members = new FakeMemberRepository();
        foreach (var member in new[] { _seller, _buyer, _otherBuyer })
            members.Items[member.Id] = member;
        _handler = new ReserveListingHandler(listings, members, _notifications, _blocks, _unitOfWork, new FixedTimeProvider(Now));
    }

    private void Block(Member blocker, Member blocked) =>
        _blocks.Add(MemberBlock.Create(blocker.Id, blocked.Id, Now).Value);

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_Reserving_Then_FailsWithMemberBlockedAndChangesNothing()
    {
        Block(_seller, _buyer);

        var result = await _handler.HandleAsync(new ReserveListingCommand(_listing.Id, _buyer.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Member.Blocked");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        _listing.Status.Should().Be(ListingStatus.Active);
        _listing.Reservations.Should().BeEmpty();
        _notifications.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_BuyerBlockedSeller_When_Reserving_Then_Succeeds()
    {
        Block(_buyer, _seller);

        var result = await _handler.HandleAsync(new ReserveListingCommand(_listing.Id, _buyer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _listing.Status.Should().Be(ListingStatus.Reserved);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SellerBlockedAnotherBuyer_When_Reserving_Then_Succeeds()
    {
        Block(_seller, _otherBuyer);

        var result = await _handler.HandleAsync(new ReserveListingCommand(_listing.Id, _buyer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}

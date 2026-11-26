using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Blocks.BlockMember;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Blocks.BlockMember;

public class BlockMemberHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly Member _blocker = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _blocked = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Member _third = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly FakeMemberRepository _members = new();
    private readonly FakeOfferRepository _offers = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly BlockMemberHandler _handler;

    public BlockMemberHandlerTests()
    {
        _members.Items[_blocker.Id] = _blocker;
        _members.Items[_blocked.Id] = _blocked;
        _members.Items[_third.Id] = _third;
        _handler = new BlockMemberHandler(_blocks, _members, _offers, _unitOfWork, new FixedTimeProvider(Now));
    }

    private static Listing Active(Guid sellerId, string title)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 100m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public async Task Given_TwoMembers_When_Blocking_Then_AddsBlockAndSavesOnce()
    {
        var result = await _handler.HandleAsync(new BlockMemberCommand(_blocker.Id, _blocked.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_blocked.Id);
        var block = _blocks.Items.Should().ContainSingle().Subject;
        block.BlockerId.Should().Be(_blocker.Id);
        block.BlockedId.Should().Be(_blocked.Id);
        block.CreatedAt.Should().Be(Now);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_OwnId_When_Blocking_Then_FailsWithBlockSelfAndSavesNothing()
    {
        var result = await _handler.HandleAsync(new BlockMemberCommand(_blocker.Id, _blocker.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Block.Self");
        result.Error.Message.Should().Be("You cannot block yourself.");
        result.Error.Type.Should().Be(ErrorType.Validation);
        _blocks.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownMember_When_Blocking_Then_FailsWithMemberNotFoundAndSavesNothing()
    {
        var result = await _handler.HandleAsync(new BlockMemberCommand(_blocker.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Match("Member '*' was not found.");
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _blocks.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AlreadyBlocked_When_BlockingAgain_Then_SucceedsWithoutSecondBlockOrSave()
    {
        var command = new BlockMemberCommand(_blocker.Id, _blocked.Id);
        await _handler.HandleAsync(command, CancellationToken.None);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_blocked.Id);
        _blocks.Items.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    // The handler has no notification repository: rejecting the offers cannot notify anyone.
    [Fact]
    public async Task Given_PendingOffersBetweenTheTwo_When_Blocking_Then_RejectsOnlyThosePendingOnes()
    {
        var blockersListing = Active(_blocker.Id, "Algebra book");
        var blockedsListing = Active(_blocked.Id, "Lab coat");
        var thirdsListing = Active(_third.Id, "Calculator");
        var asSeller = Offer.Make(blockersListing, _blocked.Id, 80m, Now).Value;
        var asBuyer = Offer.Make(blockedsListing, _blocker.Id, 50m, Now).Value;
        var answered = Offer.Make(blockersListing, _blocked.Id, 70m, Now).Value;
        answered.Reject(_blocker.Id, Now);
        var countered = Offer.Make(blockersListing, _blocked.Id, 60m, Now).Value;
        var counter = countered.Counter(_blocker.Id, blockersListing, 90m, Now).Value;
        var withThird = Offer.Make(thirdsListing, _blocker.Id, 40m, Now).Value;
        foreach (var offer in new[] { asSeller, asBuyer, answered, countered, counter, withThird })
            _offers.Add(offer);

        var result = await _handler.HandleAsync(new BlockMemberCommand(_blocker.Id, _blocked.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        asSeller.Status.Should().Be(OfferStatus.Rejected);
        asSeller.RespondedAt.Should().Be(Now);
        asBuyer.Status.Should().Be(OfferStatus.Rejected);
        asBuyer.RespondedAt.Should().Be(Now);
        answered.Status.Should().Be(OfferStatus.Rejected);
        countered.Status.Should().Be(OfferStatus.Countered);
        counter.Status.Should().Be(OfferStatus.Rejected);
        withThird.Status.Should().Be(OfferStatus.Pending);
        _unitOfWork.SaveCount.Should().Be(1);
    }
}

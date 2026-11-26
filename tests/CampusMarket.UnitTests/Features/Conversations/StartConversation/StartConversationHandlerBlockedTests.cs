using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Conversations.StartConversation;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations.StartConversation;

public class StartConversationHandlerBlockedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _buyer = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Member _otherBuyer = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly Listing _listing;
    private readonly FakeConversationRepository _conversations = new();
    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly StartConversationHandler _handler;

    public StartConversationHandlerBlockedTests()
    {
        _listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        var listings = new FakeListingRepository();
        listings.Items[_listing.Id] = _listing;
        var members = new FakeMemberRepository();
        foreach (var member in new[] { _seller, _buyer, _otherBuyer })
            members.Items[member.Id] = member;
        _handler = new StartConversationHandler(listings, members, _conversations, _blocks, new FakeMessageSpamDetector(), _unitOfWork, new FixedTimeProvider(Now));
    }

    private void Block(Member blocker, Member blocked) =>
        _blocks.Add(MemberBlock.Create(blocker.Id, blocked.Id, Now).Value);

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_StartingConversation_Then_FailsWithMemberBlockedAndSavesNothing()
    {
        Block(_seller, _buyer);

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "Mai e disponibil?"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Member.Blocked");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        _conversations.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SellerBlockedBuyerAndExistingConversation_When_BuyerWritesAgain_Then_FailsAndAddsNoMessage()
    {
        var existing = Conversation.Start(_buyer.Id, _listing, "Salut", Now.AddMinutes(-5)).Value;
        _conversations.Items.Add(existing);
        Block(_seller, _buyer);

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "Alo?"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Member.Blocked");
        existing.Messages.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_BuyerBlockedSeller_When_StartingConversation_Then_Succeeds()
    {
        Block(_buyer, _seller);

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "Salut"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _conversations.Items.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SellerBlockedAnotherBuyer_When_StartingConversation_Then_Succeeds()
    {
        Block(_seller, _otherBuyer);

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "Salut"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}

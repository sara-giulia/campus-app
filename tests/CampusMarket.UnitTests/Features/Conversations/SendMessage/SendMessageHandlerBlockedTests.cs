using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Conversations.SendMessage;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations.SendMessage;

public class SendMessageHandlerBlockedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrangerId = new("33333333-3333-3333-3333-333333333333");

    private readonly Conversation _conversation;
    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly SendMessageHandler _handler;

    public SendMessageHandlerBlockedTests()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        _conversation = Conversation.Start(BuyerId, listing, "Mai e disponibil?", Now.AddMinutes(-10)).Value;
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(_conversation);
        _handler = new SendMessageHandler(conversations, _blocks, new FakeMessageSpamDetector(), _unitOfWork, new FixedTimeProvider(Now));
    }

    private void Block(Guid blocker, Guid blocked) =>
        _blocks.Add(MemberBlock.Create(blocker, blocked, Now).Value);

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_BuyerWrites_Then_FailsWithMemberBlockedAndAddsNoMessage()
    {
        Block(SellerId, BuyerId);

        var result = await _handler.HandleAsync(new SendMessageCommand(BuyerId, _conversation.Id, "Alo?"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Member.Blocked");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        _conversation.Messages.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_BuyerBlockedSeller_When_SellerWrites_Then_FailsWithMemberBlocked()
    {
        Block(BuyerId, SellerId);

        var result = await _handler.HandleAsync(new SendMessageCommand(SellerId, _conversation.Id, "Da"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Member.Blocked");
        _conversation.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_SellerWrites_Then_BlockerCanStillWrite()
    {
        Block(SellerId, BuyerId);

        var result = await _handler.HandleAsync(new SendMessageCommand(SellerId, _conversation.Id, "Te-am blocat"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _conversation.Messages.Should().HaveCount(2);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_StrangerBlockedBySeller_When_StrangerWrites_Then_FailsWithNotParticipantNotBlocked()
    {
        Block(SellerId, StrangerId);

        var result = await _handler.HandleAsync(new SendMessageCommand(StrangerId, _conversation.Id, "Salut"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Conversation.NotParticipant");
        _conversation.Messages.Should().ContainSingle();
    }
}

using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Conversations.MarkConversationRead;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations.MarkConversationRead;

public class MarkConversationReadHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Conversation BuildConversationWithTwoBuyerMessages()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        var conversation = Conversation.Start(BuyerId, listing, "Mai e disponibil?", Now).Value;
        conversation.Send(BuyerId, "Il iau azi", Now.AddMinutes(1));
        return conversation;
    }

    [Fact]
    public async Task Given_UnreadMessages_When_SellerMarksRead_Then_ReturnsCountMarksConversationAndSavesOnce()
    {
        var conversation = BuildConversationWithTwoBuyerMessages();
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkConversationReadHandler(conversations, unitOfWork);
        var command = new MarkConversationReadCommand(SellerId, conversation.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        conversations.Items.Single().SellerLastReadNumber.Should().Be(2);
        conversations.Items.Single().BuyerLastReadNumber.Should().Be(2);
        conversations.Items.Single().UnreadCountFor(SellerId).Should().Be(0);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_NothingUnread_When_BuyerMarksRead_Then_ReturnsZeroAndDoesNotSave()
    {
        var conversation = BuildConversationWithTwoBuyerMessages();
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkConversationReadHandler(conversations, unitOfWork);
        var command = new MarkConversationReadCommand(BuyerId, conversation.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        conversation.BuyerLastReadNumber.Should().Be(2);
        conversation.SellerLastReadNumber.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownConversation_When_Handling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var conversation = BuildConversationWithTwoBuyerMessages();
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkConversationReadHandler(conversations, unitOfWork);
        var conversationId = Guid.NewGuid();
        var command = new MarkConversationReadCommand(SellerId, conversationId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Conversation.NotFound");
        result.Error.Message.Should().Be($"Conversation '{conversationId}' was not found.");
        conversation.SellerLastReadNumber.Should().Be(0);
        conversation.BuyerLastReadNumber.Should().Be(2);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_Stranger_When_Handling_Then_ReturnsForbiddenAndDoesNotSave()
    {
        var conversation = BuildConversationWithTwoBuyerMessages();
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkConversationReadHandler(conversations, unitOfWork);
        var command = new MarkConversationReadCommand(OtherMemberId, conversation.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Conversation.NotParticipant");
        result.Error.Message.Should().Be("Only the buyer and the seller can read this conversation.");
        conversation.SellerLastReadNumber.Should().Be(0);
        conversation.BuyerLastReadNumber.Should().Be(2);
        unitOfWork.SaveCount.Should().Be(0);
    }
}

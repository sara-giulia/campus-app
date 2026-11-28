using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Conversations.SendMessage;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations.SendMessage;

public class SendMessageHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Conversation BuildConversation()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return Conversation.Start(BuyerId, listing, "Mai e disponibil?", Now.AddMinutes(-10)).Value;
    }

    private static SendMessageHandler BuildHandler(FakeConversationRepository conversations, FakeUnitOfWork unitOfWork) =>
        new(conversations, new FakeMemberBlockRepository(), new FakeMemberRepository(), new FakeMessageSpamDetector(), unitOfWork, new FixedTimeProvider(Now));

    [Fact]
    public async Task Given_Conversation_When_SellerAnswers_Then_AddsMessageAndSavesOnce()
    {
        var conversation = BuildConversation();
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(conversations, unitOfWork);
        var command = new SendMessageCommand(SellerId, conversation.Id, "  Da, este  ");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ConversationId.Should().Be(conversation.Id);
        result.Value.Number.Should().Be(2);
        result.Value.SenderId.Should().Be(SellerId);
        result.Value.Text.Should().Be("Da, este");
        result.Value.SentAt.Should().Be(Now);
        conversations.Items[0].Messages.Should().HaveCount(2);
        conversations.Items[0].Messages[1].Id.Should().Be(result.Value.Id);
        conversations.Items[0].LastMessageAt.Should().Be(Now);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownConversation_When_Handling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var conversationId = Guid.NewGuid();
        var conversations = new FakeConversationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(conversations, unitOfWork);
        var command = new SendMessageCommand(SellerId, conversationId, "Da, este");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Conversation.NotFound");
        result.Error.Message.Should().Be($"Conversation '{conversationId}' was not found.");
        conversations.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_OtherMember_When_Handling_Then_ReturnsForbiddenAndAddsNothing()
    {
        var conversation = BuildConversation();
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(conversations, unitOfWork);
        var command = new SendMessageCommand(OtherMemberId, conversation.Id, "Salut");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Conversation.NotParticipant");
        result.Error.Message.Should().Be("Only the buyer and the seller can write in this conversation.");
        conversation.Messages.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyText_When_Handling_Then_ReturnsMessageEmptyAndAddsNothing()
    {
        var conversation = BuildConversation();
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(conversations, unitOfWork);
        var command = new SendMessageCommand(SellerId, conversation.Id, "   ");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.Empty");
        result.Error.Message.Should().Be("The message must not be empty.");
        conversation.Messages.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TooLongText_When_Handling_Then_ReturnsMessageTooLongAndAddsNothing()
    {
        var conversation = BuildConversation();
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(conversations, unitOfWork);
        var command = new SendMessageCommand(SellerId, conversation.Id, new string('a', Message.MaxTextLength + 1));

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.TooLong");
        result.Error.Message.Should().Be($"The message must have at most {Message.MaxTextLength} characters.");
        conversation.Messages.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(0);
    }
}

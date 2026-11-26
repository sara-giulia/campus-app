using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Conversations.SendMessage;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations.SendMessage;

public class SendMessageHandlerSpamTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrangerId = new("33333333-3333-3333-3333-333333333333");

    private readonly Conversation _conversation;
    private readonly FakeConversationRepository _conversations = new();
    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly FakeMessageSpamDetector _detector = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly SendMessageHandler _handler;

    public SendMessageHandlerSpamTests()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        _conversation = Conversation.Start(BuyerId, listing, "Mai e disponibil?", Now.AddMinutes(-10)).Value;
        _conversations.Items.Add(_conversation);
        _handler = new SendMessageHandler(_conversations, _blocks, _detector, _unitOfWork, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Given_CleanVerdict_When_SellerAnswers_Then_SavesMessageWithProbabilityAndNotSuspicious()
    {
        _detector.Result = new MessageSpamCheck(MessageSpamVerdict.Clean, 0.05);

        var result = await _handler.HandleAsync(new SendMessageCommand(SellerId, _conversation.Id, "Da, este"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuspicious.Should().BeFalse();
        _conversation.Messages[1].SpamProbability.Should().Be(0.05);
        _conversation.Messages[1].IsSuspicious.Should().BeFalse();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SuspiciousVerdict_When_SellerAnswers_Then_SavesMessageAsSuspicious()
    {
        _detector.Result = new MessageSpamCheck(MessageSpamVerdict.Suspicious, 0.7);

        var result = await _handler.HandleAsync(new SendMessageCommand(SellerId, _conversation.Id, "Intra pe www.oferta.ro"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuspicious.Should().BeTrue();
        _conversation.Messages[1].SpamProbability.Should().Be(0.7);
        _conversation.Messages[1].IsSuspicious.Should().BeTrue();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_BlockedVerdict_When_SellerAnswers_Then_ReturnsMessageSpamAndDoesNotCommit()
    {
        _detector.Result = new MessageSpamCheck(MessageSpamVerdict.Blocked, 0.99);

        var result = await _handler.HandleAsync(new SendMessageCommand(SellerId, _conversation.Id, "Trimite 150 lei avans"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.Spam");
        result.Error.Message.Should().Be("The message looks like a scam (money or card data asked for, or a suspicious link) and was not sent.");
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TextWithSpaces_When_SellerAnswers_Then_DetectorGetsTrimmedTextOnce()
    {
        await _handler.HandleAsync(new SendMessageCommand(SellerId, _conversation.Id, "  Buna  "), CancellationToken.None);

        _detector.CallCount.Should().Be(1);
        _detector.LastText.Should().Be("Buna");
    }

    [Fact]
    public async Task Given_UnknownConversation_When_Handling_Then_DetectorIsNotCalled()
    {
        var result = await _handler.HandleAsync(new SendMessageCommand(SellerId, Guid.NewGuid(), "Da, este"), CancellationToken.None);

        result.Error.Code.Should().Be("Conversation.NotFound");
        _detector.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SenderBlockedByOtherParticipant_When_Handling_Then_DetectorIsNotCalled()
    {
        _blocks.Add(MemberBlock.Create(SellerId, BuyerId, Now).Value);

        var result = await _handler.HandleAsync(new SendMessageCommand(BuyerId, _conversation.Id, "Alo?"), CancellationToken.None);

        result.Error.Code.Should().Be("Member.Blocked");
        _detector.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_Stranger_When_Handling_Then_DetectorIsNotCalled()
    {
        var result = await _handler.HandleAsync(new SendMessageCommand(StrangerId, _conversation.Id, "Salut"), CancellationToken.None);

        result.Error.Code.Should().Be("Conversation.NotParticipant");
        _detector.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyText_When_Handling_Then_DetectorIsNotCalled()
    {
        var result = await _handler.HandleAsync(new SendMessageCommand(SellerId, _conversation.Id, "   "), CancellationToken.None);

        result.Error.Code.Should().Be("Message.Empty");
        _detector.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TooLongText_When_Handling_Then_DetectorIsNotCalled()
    {
        var text = new string('a', Message.MaxTextLength + 1);

        var result = await _handler.HandleAsync(new SendMessageCommand(SellerId, _conversation.Id, text), CancellationToken.None);

        result.Error.Code.Should().Be("Message.TooLong");
        _detector.CallCount.Should().Be(0);
    }
}
